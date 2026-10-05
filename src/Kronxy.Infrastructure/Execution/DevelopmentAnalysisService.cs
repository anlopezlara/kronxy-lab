using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Execution;

namespace Kronxy.Infrastructure.Execution;

public sealed class DevelopmentAnalysisService : IDevelopmentAnalysisService
{
    private const string Version = "development-analysis-v1";
    private static readonly Regex CandidatePath = new(@"(?im)^\s*(src|tests)/[^\r\n]+\.cs\s*$", RegexOptions.Compiled);
    private static readonly Regex Declaration = new(@"\b(?:class|record|struct|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
    private readonly IArtifactStore artifactStore;
    private readonly DevelopmentAnalysisOptions options;

    public DevelopmentAnalysisService(IArtifactStore artifactStore, DevelopmentAnalysisOptions options)
    {
        this.artifactStore = artifactStore ?? throw new ArgumentNullException(nameof(artifactStore));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        options.Validate();
    }

    public async Task<DevelopmentAnalysisResult> AnalyzeAsync(DevelopmentAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        if (request.JobId == Guid.Empty || request.RunId == Guid.Empty || request.AttemptCount <= 0 ||
            string.IsNullOrWhiteSpace(request.JobRequest) || !Directory.Exists(request.Repository.RepositoryPath))
            return DevelopmentAnalysisResult.Failure("DEVELOPMENT_ANALYSIS_INVALID_REQUEST");

        string root = Path.GetFullPath(request.Repository.RepositoryPath);
        string[] candidates = CandidatePath.Matches(request.JobRequest)
            .Select(match => match.Value.Trim().Replace('\\', '/'))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(options.MaxCandidateTargets + 1).ToArray();
        if (candidates.Length == 0 || candidates.Length > options.MaxCandidateTargets)
            return await PersistUnknownAsync(request, candidates.Take(options.MaxCandidateTargets).ToArray(), "Candidate targets are missing or exceed the configured limit.", cancellationToken);

        var filesInspected = new List<string>();
        var evidence = new List<DevelopmentAnalysisEvidence>();
        var declarations = new List<string>();
        var symbols = new HashSet<string>(StringComparer.Ordinal);
        var existingCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string candidate in candidates)
        {
            string full = Path.GetFullPath(Path.Combine(root, candidate));
            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                return DevelopmentAnalysisResult.Failure("DEVELOPMENT_ANALYSIS_UNSAFE_PATH");
            if (!File.Exists(full))
            {
                symbols.Add(Path.GetFileNameWithoutExtension(candidate));
                evidence.Add(new("TargetAbsent", candidate, "Candidate path does not exist."));
                continue;
            }

            existingCandidates.Add(candidate);
            string source = await File.ReadAllTextAsync(full, cancellationToken);
            filesInspected.Add(candidate);
            foreach (Match match in Declaration.Matches(source))
            {
                string symbol = match.Groups[1].Value;
                symbols.Add(symbol);
                declarations.Add($"{symbol}@{candidate}");
                evidence.Add(new("Declaration", candidate, symbol));
            }
        }

        var impactedLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string candidate in candidates) impactedLayers.Add(Layer(candidate));
        var breakingContracts = new HashSet<string>(StringComparer.Ordinal);
        int references = 0;
        bool truncated = false;
        IEnumerable<string> sourceFiles = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                           !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .OrderBy(path => path, StringComparer.Ordinal);

        foreach (string file in sourceFiles)
        {
            if (filesInspected.Count >= options.MaxFilesInspected) { truncated = true; break; }
            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (existingCandidates.Contains(relative)) continue;
            string source = await File.ReadAllTextAsync(file, cancellationToken);
            filesInspected.Add(relative);
            foreach (string symbol in symbols)
            {
                if (!Regex.IsMatch(source, $@"\b{Regex.Escape(symbol)}\b")) continue;
                impactedLayers.Add(Layer(relative));
                if (references++ < options.MaxReferenceMatches)
                    evidence.Add(new("Reference", relative, symbol));
                else truncated = true;
                if (relative.StartsWith("src/", StringComparison.OrdinalIgnoreCase))
                    breakingContracts.Add($"{symbol} consumed by {relative}");
            }
            if (evidence.Count > options.MaxEvidenceItems) { truncated = true; break; }
        }

        string requestedScope = RequestedScope(request.JobRequest);
        string requiredScope = impactedLayers.Count <= 1 ? impactedLayers.FirstOrDefault() ?? "Unknown" : "Cross-layer";
        bool hasExisting = existingCandidates.Count > 0;
        bool allExisting = existingCandidates.Count == candidates.Length;
        bool crossLayer = impactedLayers.Count(layer => layer != "Tests") > 1;
        bool replacementIntent = Regex.IsMatch(request.JobRequest, @"(?i)replace|rename|remove|parent|belongs\s+to|exactly|only\s*:");
        bool extensionIntent = Regex.IsMatch(request.JobRequest, @"(?i)\b(add|extend|additional|new member)\b");
        bool refactorIntent = Regex.IsMatch(request.JobRequest, @"(?i)\brefactor\b");
        string[] requiredIdentifiers = RequiredIdentifiers(request.JobRequest).Distinct(StringComparer.Ordinal).ToArray();
        bool requirementsSatisfied = allExisting && requiredIdentifiers.Length > 0 && requiredIdentifiers.All(identifier =>
            candidates.Where(existingCandidates.Contains).Any(path => File.ReadAllText(Path.Combine(root, path)).Contains(identifier, StringComparison.Ordinal)));
        bool? compatible = truncated ? null : requestedScope == "Unspecified" || requestedScope == "Cross-layer" ||
            (!crossLayer && impactedLayers.Contains(requestedScope.Replace("-only", string.Empty)));

        DevelopmentChangeClassification classification;
        if (truncated) classification = DevelopmentChangeClassification.Unknown;
        else if (!hasExisting) classification = DevelopmentChangeClassification.NewComponent;
        else if (requirementsSatisfied) classification = DevelopmentChangeClassification.AlreadySatisfied;
        else if (crossLayer && requestedScope.EndsWith("-only", StringComparison.OrdinalIgnoreCase)) classification = DevelopmentChangeClassification.ArchitectureConflict;
        else if (crossLayer && replacementIntent) classification = DevelopmentChangeClassification.BreakingChange;
        else if (crossLayer) classification = DevelopmentChangeClassification.CrossLayerChange;
        else if (extensionIntent) classification = DevelopmentChangeClassification.Extension;
        else if (refactorIntent) classification = DevelopmentChangeClassification.LocalRefactor;
        else classification = DevelopmentChangeClassification.Unknown;

        bool decisionRequired = classification is DevelopmentChangeClassification.ArchitectureConflict or DevelopmentChangeClassification.Unknown || compatible != true;
        bool developerAllowed = compatible == true && !decisionRequired && classification != DevelopmentChangeClassification.AlreadySatisfied;
        var analysis = new DevelopmentAnalysis
        {
            JobId = request.JobId, RunId = request.RunId, AttemptCount = request.AttemptCount,
            RequestIdentity = Hash(request.JobRequest), TargetSymbols = symbols.Order().ToArray(),
            ExistingDeclarations = declarations.Order().ToArray(), PrimaryClassification = classification,
            ImpactedLayers = impactedLayers.Order().ToArray(), RequestedScope = requestedScope,
            RequiredScope = requiredScope, ScopeCompatible = compatible,
            BreakingContracts = breakingContracts.Take(options.MaxEvidenceItems).ToArray(),
            ArchitectureDecisionRequired = decisionRequired, DeveloperExecutionAllowed = developerAllowed,
            Evidence = evidence.Take(options.MaxEvidenceItems).ToArray(), FilesInspected = filesInspected.ToArray(),
            AnalysisVersion = Version
        };
        return await PersistAsync(request, analysis, cancellationToken);
    }

    private Task<DevelopmentAnalysisResult> PersistUnknownAsync(DevelopmentAnalysisRequest request, string[] targets, string reason, CancellationToken token) =>
        PersistAsync(request, new DevelopmentAnalysis
        {
            JobId=request.JobId, RunId=request.RunId, AttemptCount=request.AttemptCount, RequestIdentity=Hash(request.JobRequest),
            TargetSymbols=targets.Select(target => Path.GetFileNameWithoutExtension(target) ?? string.Empty).ToArray(), ExistingDeclarations=[],
            PrimaryClassification=DevelopmentChangeClassification.Unknown, ImpactedLayers=[], RequestedScope=RequestedScope(request.JobRequest),
            RequiredScope="Unknown", ScopeCompatible=null, BreakingContracts=[], ArchitectureDecisionRequired=true,
            DeveloperExecutionAllowed=false, Evidence=[new("Ambiguity", string.Empty, reason)], FilesInspected=[], AnalysisVersion=Version
        }, token);

    private async Task<DevelopmentAnalysisResult> PersistAsync(DevelopmentAnalysisRequest request, DevelopmentAnalysis analysis, CancellationToken token)
    {
        ArtifactWriteResult write = await artifactStore.WriteAsync(new ArtifactWriteRequest
        {
            JobId=request.JobId, RunId=request.RunId, ArtifactType=ArtifactType.DevelopmentAnalysis,
            Content=JsonSerializer.SerializeToUtf8Bytes(analysis), CorrelationId=request.CorrelationId
        }, token);
        return write.IsSuccess ? DevelopmentAnalysisResult.Success(analysis, write.Artifact!) : DevelopmentAnalysisResult.Failure("DEVELOPMENT_ANALYSIS_ARTIFACT_FAILED");
    }

    private static string Layer(string path) =>
        path.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase) || path.Contains("/Configurations/", StringComparison.OrdinalIgnoreCase) ? "Persistence" :
        path.StartsWith("src/Kronxy.Domain/", StringComparison.OrdinalIgnoreCase) ? "Domain" :
        path.StartsWith("src/Kronxy.Application/", StringComparison.OrdinalIgnoreCase) ? "Application" :
        path.StartsWith("src/Kronxy.Infrastructure/", StringComparison.OrdinalIgnoreCase) ? "Infrastructure" :
        path.StartsWith("src/Kronxy.Api/", StringComparison.OrdinalIgnoreCase) ? "API" :
        path.StartsWith("tests/", StringComparison.OrdinalIgnoreCase) ? "Tests" : "Other";

    private static string RequestedScope(string request) =>
        Regex.IsMatch(request, @"(?i)domain[- ]only") ? "Domain-only" :
        Regex.IsMatch(request, @"(?i)application[- ]only") ? "Application-only" :
        Regex.IsMatch(request, @"(?i)infrastructure[- ]only") ? "Infrastructure-only" :
        Regex.IsMatch(request, @"(?i)cross[- ]layer") ? "Cross-layer" : "Unspecified";

    private static IEnumerable<string> RequiredIdentifiers(string request)
    {
        bool section = false;
        foreach (string raw in request.Split('\n'))
        {
            string line = raw.Trim();
            if (Regex.IsMatch(line, @"(?i)^REQUIRED.*(FIELDS|MEMBERS|METHODS|ERRORS|CONTRACT)")) { section = true; continue; }
            if (section && line.Length == 0) { section = false; continue; }
            if (!section) continue;
            Match match = Regex.Match(line, @"([A-Za-z_][A-Za-z0-9_]*)\s*(?:\(|$)");
            if (match.Success) yield return match.Groups[1].Value;
        }
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

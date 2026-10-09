using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Execution;

namespace Kronxy.Infrastructure.Execution;

public sealed class DevelopmentAnalysisService : IDevelopmentAnalysisService
{
    private const string Version = "development-analysis-v2";
    private static readonly Regex CandidatePath = new(
        @"(?<![A-Za-z0-9_./:\\-])(?<path>(?:src|tests|tools)/(?:[A-Za-z0-9_.-]+/)*[A-Za-z0-9_.-]+\.(?:cs|razor|cshtml|json|css|js|ts|html|ya?ml|xml|props|targets|csproj|sln))(?=$|[\s`'\""()\[\]{},;:!?\.])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CandidateFileName = new(
        @"(?<![A-Za-z0-9_./:\\-])(?<name>[A-Za-z0-9_.-]+\.(?:cs|razor|cshtml|json|css|js|ts|html|ya?ml|xml|props|targets|csproj|sln))(?=$|[\s`'\""()\[\]{},;:!?\.])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Declaration = new(@"\b(?:class|record|struct|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".razor", ".cshtml", ".json", ".css", ".js", ".ts", ".html",
        ".yml", ".yaml", ".xml", ".props", ".targets", ".csproj", ".sln"
    };
    private static readonly HashSet<string> LexicalStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "as", "at", "by", "existing", "file", "for", "from", "in", "of",
        "on", "or", "page", "project", "repository", "src", "test", "tests", "the", "to", "with"
    };
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
        string[] inventory = CreateCandidateInventory(root, options.MaxFilesInspected + 1);
        if (inventory.Length > options.MaxFilesInspected)
            return await PersistUnknownAsync(request, [], "Candidate inventory exceeds the configured inspection limit.", cancellationToken);

        CandidateDiscovery discovery = DiscoverCandidates(request.JobRequest, inventory, options.MaxCandidateTargets);
        if (discovery.IsAmbiguous || discovery.Paths.Length == 0 || discovery.Paths.Length > options.MaxCandidateTargets)
            return await PersistUnknownAsync(
                request,
                discovery.Paths.Take(options.MaxCandidateTargets).ToArray(),
                discovery.Reason ?? "Candidate targets are missing or exceed the configured limit.",
                cancellationToken);

        string[] candidates = discovery.Paths;

        var filesInspected = new List<string>();
        var evidence = new List<DevelopmentAnalysisEvidence>();
        var declarations = new List<string>();
        var symbols = new HashSet<string>(StringComparer.Ordinal);
        var existingCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string candidate in candidates)
        {
            symbols.Add(Path.GetFileNameWithoutExtension(candidate));
            string full = Path.GetFullPath(Path.Combine(root, candidate));
            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                return DevelopmentAnalysisResult.Failure("DEVELOPMENT_ANALYSIS_UNSAFE_PATH");
            if (!File.Exists(full))
            {
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
        IEnumerable<string> sourceFiles = inventory
            .Select(relative => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));

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
        string[] scopeDefiningLayers = impactedLayers
            .Where(layer => !layer.Equals("Tests", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        string requiredScope = scopeDefiningLayers.Length switch
        {
            0 => "Unknown",
            1 => $"{scopeDefiningLayers[0]}-only",
            _ => "Cross-layer"
        };
        bool hasExisting = existingCandidates.Count > 0;
        bool allExisting = existingCandidates.Count == candidates.Length;
        bool crossLayer = scopeDefiningLayers.Length > 1;
        bool replacementIntent = Regex.IsMatch(request.JobRequest, @"(?i)replace|rename|remove|parent|belongs\s+to|exactly|only\s*:");
        bool extensionIntent = Regex.IsMatch(request.JobRequest, @"(?i)\b(add|extend|additional|new member)\b");
        bool refactorIntent = Regex.IsMatch(request.JobRequest, @"(?i)\brefactor\b");
        string[] requiredIdentifiers = RequiredIdentifiers(request.JobRequest).Distinct(StringComparer.Ordinal).ToArray();
        bool requirementsSatisfied = allExisting && requiredIdentifiers.Length > 0 && requiredIdentifiers.All(identifier =>
            candidates.Where(existingCandidates.Contains).Any(path => File.ReadAllText(Path.Combine(root, path)).Contains(identifier, StringComparison.Ordinal)));
        bool? compatible = truncated ? null : requestedScope == "Unspecified" || requestedScope == "Cross-layer" ||
            (!crossLayer && scopeDefiningLayers.Contains(
                requestedScope.Replace("-only", string.Empty),
                StringComparer.OrdinalIgnoreCase));

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
        path.StartsWith("src/", StringComparison.OrdinalIgnoreCase) &&
            (path.Contains(".Web/", StringComparison.OrdinalIgnoreCase) ||
             path.Contains("/Components/", StringComparison.OrdinalIgnoreCase) ||
             Path.GetExtension(path).Equals(".razor", StringComparison.OrdinalIgnoreCase) ||
             Path.GetExtension(path).Equals(".cshtml", StringComparison.OrdinalIgnoreCase)) ? "Web" :
        path.StartsWith("tests/", StringComparison.OrdinalIgnoreCase) ? "Tests" : "Other";

    private static string RequestedScope(string request) =>
        Regex.IsMatch(request, @"(?i)domain[- ]only") ? "Domain-only" :
        Regex.IsMatch(request, @"(?i)application[- ]only") ? "Application-only" :
        Regex.IsMatch(request, @"(?i)infrastructure[- ]only") ? "Infrastructure-only" :
        Regex.IsMatch(request, @"(?i)(?:web|presentation|ui)[- ]only") ? "Web-only" :
        Regex.IsMatch(request, @"(?i)cross[- ]layer") ? "Cross-layer" : "Unspecified";

    private static string[] CreateCandidateInventory(string root, int limit)
    {
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(IsSafeCandidatePath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
    }

    private static bool IsSafeCandidatePath(string path)
    {
        if (!(path.StartsWith("src/", StringComparison.OrdinalIgnoreCase) ||
              path.StartsWith("tests/", StringComparison.OrdinalIgnoreCase) ||
              path.StartsWith("tools/", StringComparison.OrdinalIgnoreCase) ||
              !path.Contains('/')) ||
            !SupportedExtensions.Contains(Path.GetExtension(path)))
            return false;

        string[] segments = path.Split('/');
        return !segments.Any(segment =>
            segment is "." or ".." ||
            segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("artifacts", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("workspaces", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
            segment.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
            segment.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase));
    }

    private static CandidateDiscovery DiscoverCandidates(string request, string[] inventory, int limit)
    {
        string[] explicitPaths = CandidatePath.Matches(request)
            .Select(match => match.Groups["path"].Value.Replace('\\', '/'))
            .Where(IsSafeCandidatePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(limit + 1)
            .ToArray();
        if (explicitPaths.Length != 0)
            return explicitPaths.Length > limit
                ? new(explicitPaths, true, "Candidate targets exceed the configured limit.")
                : new(explicitPaths, false, null);

        string[] names = CandidateFileName.Matches(request)
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] namedMatches = inventory
            .Where(path => names.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            .Take(limit + 1)
            .ToArray();
        if (namedMatches.Length != 0)
            return namedMatches.Length > 1
                ? new(namedMatches, true, "The explicit filename matches multiple candidate targets.")
                : new(namedMatches, false, null);

        HashSet<string> requestTokens = Tokenize(request).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool stylingIntent = Regex.IsMatch(request, @"(?i)\b(css|style|styling|stylesheet|layout|theme)\b");
        bool scriptIntent = Regex.IsMatch(request, @"(?i)\b(java\s*script|typescript|script|client[- ]side|browser behavior)\b");
        var ranked = inventory
            .Select(path => new { Path = path, Score = LexicalScore(path, requestTokens, stylingIntent, scriptIntent) })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .ToArray();
        if (ranked.Length == 0)
            return new([], false, "No meaningful candidate target was discovered from the bounded source inventory.");

        int bestScore = ranked[0].Score;
        string[] best = ranked.Where(item => item.Score == bestScore).Select(item => item.Path).Take(limit + 1).ToArray();
        string[] componentKeys = best.Select(ComponentKey).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (best.Length > limit || componentKeys.Length > 1)
            return new(best, true, "Multiple unrelated candidate targets have equal relevance or exceed the configured limit.");

        return new(best, false, null);
    }

    private static int LexicalScore(string path, HashSet<string> requestTokens, bool stylingIntent, bool scriptIntent)
    {
        string extension = Path.GetExtension(path);
        if (extension.Equals(".css", StringComparison.OrdinalIgnoreCase) && !stylingIntent)
            return 0;
        if ((extension.Equals(".js", StringComparison.OrdinalIgnoreCase) || extension.Equals(".ts", StringComparison.OrdinalIgnoreCase)) && !scriptIntent)
            return 0;

        string[] stemTokens = Tokenize(Path.GetFileNameWithoutExtension(path))
            .Where(token => !LexicalStopWords.Contains(token))
            .ToArray();
        int matched = stemTokens.Count(requestTokens.Contains);
        if (matched == 0 || (stemTokens.Length > 1 && matched < 2) ||
            (stemTokens.Length == 1 && stemTokens[0].Length < 4))
            return 0;

        int score = matched * 10;
        if (matched == stemTokens.Length)
            score += 5;
        if (path.StartsWith("tests/", StringComparison.OrdinalIgnoreCase))
            score -= 2;
        return score;
    }

    private static IEnumerable<string> Tokenize(string value)
    {
        foreach (Match match in Regex.Matches(value, @"[A-Z]+(?=[A-Z][a-z]|\b)|[A-Z]?[a-z]+|[0-9]+"))
        {
            string token = match.Value.ToLowerInvariant();
            if (!LexicalStopWords.Contains(token))
                yield return token;
        }
    }

    private static string ComponentKey(string path)
    {
        string stem = Path.GetFileNameWithoutExtension(path);
        return string.Concat(Tokenize(stem).Where(token => !token.Equals("test", StringComparison.OrdinalIgnoreCase) &&
                                                          !token.Equals("tests", StringComparison.OrdinalIgnoreCase)));
    }

    private sealed record CandidateDiscovery(string[] Paths, bool IsAmbiguous, string? Reason);

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

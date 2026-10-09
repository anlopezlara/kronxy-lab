using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Execution;

namespace Kronxy.Infrastructure.Execution;

public sealed class DevelopmentAnalysisService : IDevelopmentAnalysisService
{
    private const string Version = "development-analysis-v5";
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
    private static readonly HashSet<string> LowInformationTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "add", "data", "get", "job", "model", "service", "set"
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
            return await PersistUnknownAsync(request, "Candidate inventory exceeds the configured inspection limit.", cancellationToken);

        CandidateDiscovery discovery = DiscoverCandidates(request.JobRequest, inventory, options.MaxCandidateTargets);
        if (discovery.IsAmbiguous || discovery.Paths.Length == 0 || discovery.Paths.Length > options.MaxCandidateTargets)
            return await PersistUnknownAsync(
                request,
                discovery.Reason ?? "Candidate targets are missing or exceed the configured limit.",
                cancellationToken);

        string[] candidates = discovery.Paths;

        var filesInspected = new List<string>();
        var evidence = new List<DevelopmentAnalysisEvidence>();
        var declarations = new List<string>();
        var symbols = new HashSet<string>(StringComparer.Ordinal);
        var existingCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var authoritativeSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
            authoritativeSources[candidate] = source;
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
        bool replacementIntent = Regex.IsMatch(request.JobRequest, @"(?i)replace|rename|remove|parent|belongs\s+to|exactly|only\s*:");
        bool csharpAuthoritative = candidates.Any(path =>
            Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase));
        string[] highConfidenceSymbols = symbols
            .Where(IsHighConfidenceSymbol)
            .ToArray();
        int referenceBudget = Math.Min(
            options.MaxReferenceMatches,
            Math.Max(4, options.MaxCandidateTargets * 2));
        int remainingInspectionBudget = Math.Max(0, options.MaxFilesInspected - filesInspected.Count);
        int effectiveReferenceBudget = Math.Min(referenceBudget, remainingInspectionBudget);
        string[] relatedPaths = csharpAuthoritative
            ? StrongReferenceCandidates(inventory, existingCandidates, highConfidenceSymbols, replacementIntent, effectiveReferenceBudget + 1)
            : SupportingContextCandidates(inventory, existingCandidates, authoritativeSources, effectiveReferenceBudget + 1);

        if (relatedPaths.Length > effectiveReferenceBudget)
            return await PersistUnknownAsync(
                request,
                "Reference expansion exceeds the configured bounded inspection budget.",
                cancellationToken,
                filesInspected,
                highConfidenceSymbols);

        foreach (string relative in relatedPaths)
        {
            string file = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            string source = await File.ReadAllTextAsync(file, cancellationToken);
            filesInspected.Add(relative);
            foreach (string symbol in highConfidenceSymbols)
            {
                if (!Regex.IsMatch(source, $@"\b{Regex.Escape(symbol)}\b")) continue;
                if (references++ < options.MaxReferenceMatches)
                    evidence.Add(new("Reference", relative, symbol));
                else truncated = true;
                if (csharpAuthoritative)
                    impactedLayers.Add(Layer(relative));
                if (csharpAuthoritative && replacementIntent &&
                    relative.StartsWith("src/", StringComparison.OrdinalIgnoreCase))
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

    private Task<DevelopmentAnalysisResult> PersistUnknownAsync(
        DevelopmentAnalysisRequest request,
        string reason,
        CancellationToken token,
        IReadOnlyList<string>? filesInspected = null,
        IReadOnlyList<string>? targetSymbols = null) =>
        PersistAsync(request, new DevelopmentAnalysis
        {
            JobId=request.JobId, RunId=request.RunId, AttemptCount=request.AttemptCount, RequestIdentity=Hash(request.JobRequest),
            TargetSymbols=targetSymbols ?? [], ExistingDeclarations=[],
            PrimaryClassification=DevelopmentChangeClassification.Unknown, ImpactedLayers=[], RequestedScope=RequestedScope(request.JobRequest),
            RequiredScope="Unknown", ScopeCompatible=null, BreakingContracts=[], ArchitectureDecisionRequired=true,
            DeveloperExecutionAllowed=false, Evidence=[new("Ambiguity", string.Empty, reason)], FilesInspected=filesInspected ?? [], AnalysisVersion=Version
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
        Regex.IsMatch(request, @"(?is)expected\s+scope\s*:\s*[^\n]*\bweb\b[^\n]*\bonly\b") ? "Web-only" :
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

    private static bool IsHighConfidenceSymbol(string symbol)
    {
        string[] tokens = Tokenize(symbol).ToArray();
        if (symbol.Length < 4 || tokens.Length == 0 || tokens.All(LowInformationTokens.Contains))
            return false;
        if (Regex.IsMatch(symbol, @"^\d{8,}[_-]") ||
            tokens.Contains("designer", StringComparer.OrdinalIgnoreCase) ||
            tokens.Contains("migration", StringComparer.OrdinalIgnoreCase))
            return false;
        return !symbol.Equals("Jobs", StringComparison.OrdinalIgnoreCase) &&
               !symbol.Equals("Services", StringComparison.OrdinalIgnoreCase) &&
               !symbol.Equals("Models", StringComparison.OrdinalIgnoreCase) &&
               !symbol.Equals("Data", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] StrongReferenceCandidates(
        string[] inventory,
        HashSet<string> authoritative,
        string[] symbols,
        bool replacementIntent,
        int limit)
    {
        string[] normalizedSymbols = symbols
            .Select(NormalizeCompound)
            .Where(symbol => symbol.Length >= 4)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] semanticSuffixes = symbols
            .SelectMany(Tokenize)
            .Where(token => token.Length >= 4 && !LowInformationTokens.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return inventory
            .Where(path => !authoritative.Contains(path) &&
                           Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(path =>
            {
                string fileName = NormalizeCompound(Path.GetFileNameWithoutExtension(path));
                bool symbolAffinity = normalizedSymbols.Any(symbol => fileName.Contains(symbol, StringComparison.OrdinalIgnoreCase)) ||
                    semanticSuffixes.Any(suffix => fileName.Contains(suffix, StringComparison.OrdinalIgnoreCase));
                bool structuralReplacement = replacementIntent &&
                    (path.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase) ||
                     path.Contains("/Configurations/", StringComparison.OrdinalIgnoreCase));
                return symbolAffinity || structuralReplacement;
            })
            .OrderBy(path => path, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
    }

    private static string[] SupportingContextCandidates(
        string[] inventory,
        HashSet<string> authoritative,
        IReadOnlyDictionary<string, string> authoritativeSources,
        int limit)
    {
        string[] componentNames = authoritative
            .Select(path => NormalizeCompound(Path.GetFileNameWithoutExtension(path)))
            .Where(name => name.Length >= 5)
            .ToArray();
        string[] authoritativeProjects = authoritative
            .Select(ProjectSegment)
            .Where(project => project.Length != 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] directlyUsedTypes = authoritativeSources.Values
            .SelectMany(source => Regex.Matches(source, @"\b[A-Z][A-Za-z0-9]*(?:Client|Service)\b")
                .Select(match => match.Value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return inventory
            .Where(path => !authoritative.Contains(path))
            .Where(path =>
            {
                string fileName = NormalizeCompound(Path.GetFileNameWithoutExtension(path));
                string project = ProjectSegment(path);
                bool sameProjectFamily = authoritativeProjects.Any(authoritativeProject =>
                    project.Equals(authoritativeProject, StringComparison.OrdinalIgnoreCase) ||
                    project.StartsWith(authoritativeProject + ".Tests", StringComparison.OrdinalIgnoreCase));
                bool componentCompanion = componentNames.Any(component =>
                    fileName.Contains(component, StringComparison.OrdinalIgnoreCase)) && sameProjectFamily;
                bool directDependency = directlyUsedTypes.Any(type =>
                    fileName.Equals(NormalizeCompound(type), StringComparison.OrdinalIgnoreCase));
                return componentCompanion || directDependency;
            })
            .OrderBy(path => path, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
    }

    private static string NormalizeCompound(string value) =>
        string.Concat(Tokenize(value));

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

        RequestHints hints = RequestHints.Create(request);
        var ranked = inventory
            .Select(path => new { Path = path, Score = ScoreCandidate(path, hints).FinalScore })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .ToArray();
        if (ranked.Length == 0)
            return new([], false, "No meaningful candidate target was discovered from the bounded source inventory.");

        int bestScore = ranked[0].Score;
        string[] best = ranked.Where(item => item.Score == bestScore).Select(item => item.Path).Take(limit + 1).ToArray();
        if (best.Length > 1)
            return new(best, true, "Multiple candidate targets remain tied after structural relevance ranking.");

        return new(best, false, null);
    }

    internal static IReadOnlyList<CandidateRankingDiagnostic> RankCandidatesForDiagnostics(
        string request,
        IEnumerable<string> inventory)
    {
        RequestHints hints = RequestHints.Create(request);
        return inventory
            .Select(path => ScoreCandidate(path, hints))
            .Where(item => item.FinalScore > 0)
            .OrderByDescending(item => item.FinalScore)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .Select((item, index) => item with { FinalRank = index + 1 })
            .ToArray();
    }

    internal static IReadOnlyList<CandidateRankingDiagnostic> RankRepositoryForDiagnostics(
        string request,
        string repositoryRoot,
        int inventoryLimit = 1000) =>
        RankCandidatesForDiagnostics(
            request,
            CreateCandidateInventory(Path.GetFullPath(repositoryRoot), inventoryLimit));

    private static CandidateRankingDiagnostic ScoreCandidate(string path, RequestHints hints)
    {
        string extension = Path.GetExtension(path);
        if (extension.Equals(".css", StringComparison.OrdinalIgnoreCase) && !hints.Styling)
            return CandidateRankingDiagnostic.Zero(path);
        if ((extension.Equals(".js", StringComparison.OrdinalIgnoreCase) || extension.Equals(".ts", StringComparison.OrdinalIgnoreCase)) && !hints.Script)
            return CandidateRankingDiagnostic.Zero(path);

        string stem = Path.GetFileNameWithoutExtension(path);
        string[] stemTokens = Tokenize(stem)
            .Where(token => !LexicalStopWords.Contains(token))
            .ToArray();
        string stemCompound = string.Concat(TokenizeRaw(stem).Where(token => !long.TryParse(token, out _)));
        bool exactCompound = stemCompound.Length >= 5 && hints.Compact.Contains(stemCompound, StringComparison.OrdinalIgnoreCase);
        string[] matchedTokens = stemTokens.Where(hints.Tokens.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        int informativeMatches = matchedTokens.Count(token => !LowInformationTokens.Contains(token));

        int informativeTokenContribution = matchedTokens.Sum(token => LowInformationTokens.Contains(token) ? 1 : 7);
        int compoundMatchContribution = exactCompound ? 60 : 0;

        string project = ProjectSegment(path);
        string projectCompound = string.Concat(TokenizeRaw(project));
        bool projectAffinity = projectCompound.Length >= 4 &&
            hints.Compact.Contains(projectCompound, StringComparison.OrdinalIgnoreCase);
        int projectAffinityContribution = projectAffinity ? 40 : 0;

        string[] directoryTokens = path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Skip(2)
            .SkipLast(1)
            .SelectMany(Tokenize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        int pathAffinityContribution = Math.Min(8, directoryTokens.Count(hints.Tokens.Contains) * 2);

        bool presentationFile = extension.Equals(".razor", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".cshtml", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".html", StringComparison.OrdinalIgnoreCase);
        bool webProject = Tokenize(project).Any(token => token.Equals("web", StringComparison.OrdinalIgnoreCase) ||
                                                        token.Equals("ui", StringComparison.OrdinalIgnoreCase));
        int fileRoleContribution = 0;
        if (hints.Presentation && presentationFile)
            fileRoleContribution += 30;
        if (hints.Presentation && webProject)
            fileRoleContribution += 12;
        if (hints.Client && (stemTokens.Contains("client", StringComparer.OrdinalIgnoreCase) ||
                             directoryTokens.Contains("clients", StringComparer.OrdinalIgnoreCase)))
            fileRoleContribution += 24;
        if (hints.Test && path.StartsWith("tests/", StringComparison.OrdinalIgnoreCase))
            fileRoleContribution += 24;
        if (hints.Styling && extension.Equals(".css", StringComparison.OrdinalIgnoreCase))
            fileRoleContribution += 24;
        if (hints.Script && (extension.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
                             extension.Equals(".ts", StringComparison.OrdinalIgnoreCase)))
            fileRoleContribution += 24;

        bool migration = path.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(stem, @"^\d{8,}[_-]");
        bool designer = stem.EndsWith(".Designer", StringComparison.OrdinalIgnoreCase) ||
            stemTokens.Contains("designer", StringComparer.OrdinalIgnoreCase);
        bool snapshot = stemTokens.Contains("snapshot", StringComparer.OrdinalIgnoreCase);
        bool assemblyMetadata = stem.StartsWith("Assembly", StringComparison.OrdinalIgnoreCase) &&
            (stem.Contains("Info", StringComparison.OrdinalIgnoreCase) || stem.Contains("Attributes", StringComparison.OrdinalIgnoreCase));

        int migrationGeneratedContribution = migration ? (hints.Persistence ? 35 : -55) : 0;
        if (hints.Persistence && (migration || directoryTokens.Contains("persistence", StringComparer.OrdinalIgnoreCase) ||
                                  directoryTokens.Contains("repositories", StringComparer.OrdinalIgnoreCase)))
            fileRoleContribution += 20;
        int noisePenalty = 0;
        if (designer && !hints.Designer)
            noisePenalty -= 30;
        if (snapshot && !hints.Persistence)
            noisePenalty -= 35;
        if (assemblyMetadata)
            noisePenalty -= 50;
        int testPenalty = path.StartsWith("tests/", StringComparison.OrdinalIgnoreCase) && !hints.Test ? -12 : 0;

        bool hasStrongSignal = exactCompound || projectAffinity || informativeMatches > 0 ||
            (hints.Presentation && presentationFile) ||
            (hints.Persistence && migration);
        int finalScore = hasStrongSignal
            ? Math.Max(0, compoundMatchContribution + informativeTokenContribution + projectAffinityContribution +
                pathAffinityContribution + fileRoleContribution + noisePenalty + testPenalty + migrationGeneratedContribution)
            : 0;
        return new(
            path,
            finalScore,
            compoundMatchContribution,
            informativeTokenContribution,
            projectAffinityContribution,
            pathAffinityContribution,
            fileRoleContribution,
            noisePenalty,
            testPenalty,
            migrationGeneratedContribution,
            0,
            0);
    }

    private static IEnumerable<string> Tokenize(string value)
    {
        return TokenizeRaw(value).Where(token => !LexicalStopWords.Contains(token));
    }

    private static IEnumerable<string> TokenizeRaw(string value)
    {
        foreach (Match match in Regex.Matches(value, @"[A-Z]+(?=[A-Z][a-z]|\b)|[A-Z]?[a-z]+|[0-9]+"))
        {
            yield return match.Value.ToLowerInvariant();
        }
    }

    private static string ProjectSegment(string path)
    {
        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 && (segments[0].Equals("src", StringComparison.OrdinalIgnoreCase) ||
                                        segments[0].Equals("tests", StringComparison.OrdinalIgnoreCase) ||
                                        segments[0].Equals("tools", StringComparison.OrdinalIgnoreCase))
            ? segments[1]
            : string.Empty;
    }

    private sealed record CandidateDiscovery(string[] Paths, bool IsAmbiguous, string? Reason);

    internal sealed record CandidateRankingDiagnostic(
        string Path,
        int FinalScore,
        int CompoundMatchContribution,
        int InformativeTokenContribution,
        int ProjectAffinityContribution,
        int PathAffinityContribution,
        int FileRoleContribution,
        int NoisePenalty,
        int TestPenalty,
        int MigrationGeneratedContribution,
        int ExplicitContribution,
        int FinalRank)
    {
        public static CandidateRankingDiagnostic Zero(string path) =>
            new(path, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    private sealed record RequestHints(
        HashSet<string> Tokens,
        string Compact,
        bool Presentation,
        bool Client,
        bool Persistence,
        bool Test,
        bool Styling,
        bool Script,
        bool Designer)
    {
        public static RequestHints Create(string request)
        {
            string primaryRequest = PrimaryRequestText(request);
            string[] orderedTokens = TokenizeRaw(primaryRequest).ToArray();
            HashSet<string> tokens = orderedTokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
            string compact = string.Concat(orderedTokens);
            return new(
                tokens,
                compact,
                HasAny(tokens, "page", "view", "screen", "ui", "frontend", "presentation", "web"),
                HasAny(tokens, "client") || (tokens.Contains("api") && tokens.Contains("client")),
                HasAny(tokens, "migration", "schema", "database", "ef", "persistence", "repository"),
                HasAny(tokens, "test", "tests", "spec", "specification"),
                HasAny(tokens, "css", "style", "styling", "stylesheet", "layout", "theme"),
                HasAny(tokens, "javascript", "typescript", "script", "browser"),
                HasAny(tokens, "designer"));
        }

        private static string PrimaryRequestText(string request)
        {
            string[] lines = request.Split('\n');
            int sectionStart = Array.FindIndex(lines, line =>
                Regex.IsMatch(line.Trim(), @"(?i)^(requirements|acceptance|expected\s+scope)\s*:"));
            return sectionStart >= 0
                ? string.Join('\n', lines.Take(sectionStart))
                : request;
        }

        private static bool HasAny(HashSet<string> tokens, params string[] values) =>
            values.Any(tokens.Contains);
    }

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

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;

namespace Kronxy.Infrastructure.Execution;

public static class BuildCorrectionEvidenceCompactor
{
    private static readonly Regex CompilerError = new(
        @"^(?<path>.+?)\((?<line>\d+),(?<column>\d+)\): error (?<code>[A-Z]{2}\d+): (?<message>.*?)(?: \[[^\]]+\])?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex TypeDeclaration = new(
        @"^[ \t]*(?<modifiers>(?:(?:public|internal|protected|private|sealed|abstract|static|partial|readonly|ref|file)\s+)*)?(?<kind>class|interface|record(?:\s+(?:class|struct))?|struct)\s+(?<name>@?[A-Za-z_][A-Za-z0-9_]*)(?<tail>[^\r\n{;]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);
    private static readonly Regex Identifier = new(
        @"(?<![A-Za-z0-9_])@?[A-Za-z_][A-Za-z0-9_]*(?![A-Za-z0-9_])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Whitespace = new(
        @"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static Task<BuildCorrectionEvidenceResult> CreateAsync(
        DeveloperBuildCorrectionContext correction,
        RepositoryWorktreeHandle repository,
        int maxCharacters,
        CancellationToken cancellationToken = default) =>
        CreateAsync(
            correction,
            repository,
            maxCharacters,
            correction.OriginalProposal.Changes.Select(x => x.RelativePath).ToArray(),
            [],
            cancellationToken);

    public static async Task<BuildCorrectionEvidenceResult> CreateAsync(
        DeveloperBuildCorrectionContext correction,
        RepositoryWorktreeHandle repository,
        int maxCharacters,
        IReadOnlyList<string> allowedPaths,
        IReadOnlyList<string> referencePaths,
        CancellationToken cancellationToken = default)
    {
        if (maxCharacters <= 0 || correction.OriginalProposal.Changes.Count == 0)
            return BuildCorrectionEvidenceResult.Failure("DEVELOPER_BUILD_CORRECTION_EVIDENCE_TOO_LARGE");

        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repository.RepositoryPath));
        if (!Directory.Exists(root) || IsLink(root))
            return BuildCorrectionEvidenceResult.Failure("DEVELOPER_BUILD_CORRECTION_SOURCE_UNSAFE");

        var source = new List<object>();
        var proposalPaths = new List<string>();
        var governedFiles = new Dictionary<string, GovernedFile>(PathComparer);
        var allowed = new HashSet<string>(
            allowedPaths.Select(NormalizePath).Where(SafeRelativePath), PathComparer);
        var references = new HashSet<string>(
            referencePaths.Select(NormalizePath).Where(SafeRelativePath), PathComparer);

        foreach (ValidatedDeveloperChange change in correction.OriginalProposal.Changes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string relative = NormalizePath(change.RelativePath);
            FileReadResult read = await ReadAuthorizedFileAsync(root, relative, cancellationToken).ConfigureAwait(false);
            if (!read.IsSuccess)
                return BuildCorrectionEvidenceResult.Failure(read.ErrorCode);
            if (!string.Equals(read.Content, change.Content, StringComparison.Ordinal))
                return BuildCorrectionEvidenceResult.Failure("DEVELOPER_BUILD_CORRECTION_SOURCE_MISMATCH");

            proposalPaths.Add(relative);
            source.Add(new { Path = relative, Content = read.Content });
            if (allowed.Contains(relative))
                governedFiles[relative] = new GovernedFile(relative, read.Content, true);
        }

        foreach (string relative in allowed.Concat(references)
                     .Distinct(PathComparer).OrderBy(path => path, StringComparer.Ordinal))
        {
            if (governedFiles.ContainsKey(relative))
                continue;
            cancellationToken.ThrowIfCancellationRequested();
            FileReadResult read = await ReadAuthorizedFileAsync(root, relative, cancellationToken).ConfigureAwait(false);
            if (read.IsSuccess)
                governedFiles[relative] = new GovernedFile(relative, read.Content, allowed.Contains(relative));
        }

        var diagnostics = new Dictionary<string, MutableDiagnostic>(StringComparer.Ordinal);
        foreach (string rawLine in correction.BuildStandardOutput.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            Match match = CompilerError.Match(line);
            if (!match.Success)
            {
                if (line.Contains(": error ", StringComparison.Ordinal))
                    return BuildCorrectionEvidenceResult.Failure("DEVELOPER_BUILD_DIAGNOSTIC_UNREPRESENTABLE");
                continue;
            }

            string rawPath = NormalizePath(match.Groups["path"].Value);
            string path = rawPath;
            string normalizedRoot = NormalizePath(root);
            if (rawPath.StartsWith(normalizedRoot + "/", PathComparison))
                path = rawPath[(normalizedRoot.Length + 1)..];
            else if (Path.IsPathFullyQualified(rawPath))
                return BuildCorrectionEvidenceResult.Failure("DEVELOPER_BUILD_DIAGNOSTIC_OUTSIDE_REPOSITORY");
            if (!SafeRelativePath(path))
                return BuildCorrectionEvidenceResult.Failure("DEVELOPER_BUILD_DIAGNOSTIC_UNREPRESENTABLE");

            string code = match.Groups["code"].Value;
            int lineNumber = int.Parse(match.Groups["line"].Value);
            int column = int.Parse(match.Groups["column"].Value);
            string message = match.Groups["message"].Value.Trim();
            string key = string.Join("\u001f", code, path, lineNumber, column, message);
            if (diagnostics.TryGetValue(key, out MutableDiagnostic? existing))
                existing.Count++;
            else
                diagnostics.Add(key, new MutableDiagnostic(code, path, lineNumber, column, message));
        }

        if (diagnostics.Count == 0)
            return BuildCorrectionEvidenceResult.Failure("DEVELOPER_BUILD_DIAGNOSTIC_MISSING");

        MutableDiagnostic[] ordered = diagnostics.Values
            .OrderBy(item => proposalPaths.Contains(item.Path, PathComparer) ? 0 : 1)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.Line).ThenBy(item => item.Column)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.Message, StringComparer.Ordinal)
            .ToArray();
        Declaration[] declarations = governedFiles.Values
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .SelectMany(ParseDeclarations).ToArray();
        RelatedDeclaration[] related = ResolveRelatedDeclarations(ordered, declarations);

        string Serialize(int included, bool includeRelated, bool includeReferences) => JsonSerializer.Serialize(new
        {
            PreviousProposalPaths = proposalPaths,
            PreviousNoOpCorrection = correction.PreviousNoOpPaths.Count == 0 ? null : new
            {
                Message = "PREVIOUS CORRECTION WAS A NO-OP. ReplaceFile left the governed source unchanged. Build errors remain unresolved. Modify source in an allowed file; inspect the declarations in the diagnostics and reference patterns. Do not repeat unchanged content.",
                Paths = correction.PreviousNoOpPaths
            },
            FailedBuild = new
            {
                correction.FailedBuildReport.ExitCode,
                correction.FailedBuildReport.ErrorCode
            },
            EffectiveSource = source,
            Diagnostics = ordered.Take(included).Select(item => new
            {
                item.Code, item.Path, item.Line, item.Column, item.Message, item.Count,
                RelatedSymbols = includeRelated ? RelatedSymbols(item, related) : []
            }),
            RelatedDeclarations = includeRelated ? related.Select(item => new
            {
                item.Symbol,
                item.Status,
                Declaration = item.Declaration is null ? null : new
                {
                    item.Declaration.Path,
                    item.Declaration.Signature,
                    item.Declaration.CanModify
                },
                ReferencePattern = includeReferences && item.ReferencePattern is not null ? new
                {
                    item.ReferencePattern.Path,
                    item.ReferencePattern.Signature
                } : null
            }) : [],
            OmittedRelatedDeclarations = includeRelated ? 0 : related.Length,
            OmittedUniqueDiagnostics = ordered.Length - included
        });

        int kept = 0;
        bool keptRelated = false;
        bool keptReferences = false;
        string content = string.Empty;
        foreach ((bool includeRelated, bool includeReferences) in
                 new[] { (true, true), (true, false), (false, false) })
        {
            for (int count = 1; count <= ordered.Length; count++)
            {
                string candidate = Serialize(count, includeRelated, includeReferences);
                if (candidate.Length > maxCharacters)
                    break;
                if (count > kept ||
                    count == kept && includeRelated && !keptRelated ||
                    count == kept && includeRelated == keptRelated && includeReferences && !keptReferences)
                {
                    kept = count;
                    keptRelated = includeRelated;
                    keptReferences = includeReferences;
                    content = candidate;
                }
            }
        }

        if (kept == 0)
            return BuildCorrectionEvidenceResult.Failure("DEVELOPER_BUILD_CORRECTION_EVIDENCE_TOO_LARGE");

        int diagnosticCharacters = JsonSerializer.Serialize(
            ordered.Take(kept).Select(item => new
            {
                item.Code, item.Path, item.Line, item.Column, item.Message, item.Count
            })).Length;
        int relatedCharacters = keptRelated
            ? content.Length - Serialize(kept, false, false).Length
            : 0;
        return BuildCorrectionEvidenceResult.Success(
            content, ordered.Length, kept,
            ordered.Sum(item => item.Count) - ordered.Length,
            diagnosticCharacters, relatedCharacters);
    }

    private static IEnumerable<Declaration> ParseDeclarations(GovernedFile file)
    {
        foreach (Match match in TypeDeclaration.Matches(file.Content))
        {
            string modifiers = NormalizeWhitespace(match.Groups["modifiers"].Value);
            string kind = NormalizeWhitespace(match.Groups["kind"].Value);
            string name = match.Groups["name"].Value.TrimStart('@');
            string tail = NormalizeWhitespace(match.Groups["tail"].Value);
            string signature = string.Join(' ', new[] { modifiers, kind, name, tail }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            yield return new Declaration(name, kind, tail, file.Path, signature, file.CanModify);
        }
    }

    private static RelatedDeclaration[] ResolveRelatedDeclarations(
        IReadOnlyList<MutableDiagnostic> diagnostics,
        IReadOnlyList<Declaration> declarations)
    {
        string[] symbols = diagnostics
            .SelectMany(item => Identifier.Matches(item.Message)
                .Select(match => match.Value.TrimStart('@')))
            .Where(symbol => declarations.Any(declaration =>
                string.Equals(declaration.Name, symbol, StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal).OrderBy(symbol => symbol, StringComparer.Ordinal)
            .ToArray();

        return symbols.Select(symbol =>
        {
            Declaration[] matches = declarations.Where(item =>
                string.Equals(item.Name, symbol, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1)
                return new RelatedDeclaration(symbol, "AMBIGUOUS", null, null);

            Declaration declaration = matches[0];
            Declaration[] patterns = declarations.Where(item =>
                !item.CanModify &&
                !string.Equals(item.Name, declaration.Name, StringComparison.Ordinal) &&
                string.Equals(item.Kind, declaration.Kind, StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(declaration.Tail) &&
                string.Equals(item.Tail, declaration.Tail, StringComparison.Ordinal)).ToArray();
            return new RelatedDeclaration(
                symbol, "RESOLVED", declaration, patterns.Length == 1 ? patterns[0] : null);
        }).ToArray();
    }

    private static string[] RelatedSymbols(
        MutableDiagnostic diagnostic,
        IReadOnlyList<RelatedDeclaration> related) =>
        Identifier.Matches(diagnostic.Message)
            .Select(match => match.Value.TrimStart('@'))
            .Where(symbol => related.Any(item =>
                string.Equals(item.Symbol, symbol, StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal).OrderBy(symbol => symbol, StringComparer.Ordinal)
            .ToArray();

    private static async Task<FileReadResult> ReadAuthorizedFileAsync(
        string root, string relative, CancellationToken cancellationToken)
    {
        if (!SafeRelativePath(relative))
            return FileReadResult.Failure("DEVELOPER_BUILD_CORRECTION_SOURCE_UNSAFE");
        string full = Path.GetFullPath(
            Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, PathComparison) ||
            HasLinkBetween(root, full) || !File.Exists(full))
            return FileReadResult.Failure("DEVELOPER_BUILD_CORRECTION_SOURCE_UNSAFE");

        try
        {
            string content = StrictUtf8.GetString(
                await File.ReadAllBytesAsync(full, cancellationToken).ConfigureAwait(false));
            return FileReadResult.Success(content);
        }
        catch (DecoderFallbackException)
        {
            return FileReadResult.Failure("DEVELOPER_BUILD_CORRECTION_SOURCE_UNSAFE");
        }
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');
    private static string NormalizeWhitespace(string value) => Whitespace.Replace(value.Trim(), " ");
    private static bool SafeRelativePath(string path) =>
        !string.IsNullOrWhiteSpace(path) && !path.StartsWith('/') &&
        !Path.IsPathFullyQualified(path) && path.Split('/').All(segment =>
            !string.IsNullOrWhiteSpace(segment) && segment is not "." and not "..");

    private static bool HasLinkBetween(string root, string full)
    {
        string current = root;
        foreach (string segment in Path.GetRelativePath(root, full).Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if ((File.Exists(current) || Directory.Exists(current)) && IsLink(current))
                return true;
        }
        return false;
    }

    private static bool IsLink(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private sealed class MutableDiagnostic(string code, string path, int line, int column, string message)
    {
        public string Code { get; } = code;
        public string Path { get; } = path;
        public int Line { get; } = line;
        public int Column { get; } = column;
        public string Message { get; } = message;
        public int Count { get; set; } = 1;
    }

    private sealed record GovernedFile(string Path, string Content, bool CanModify);
    private sealed record Declaration(
        string Name, string Kind, string Tail, string Path, string Signature, bool CanModify);
    private sealed record RelatedDeclaration(
        string Symbol, string Status, Declaration? Declaration, Declaration? ReferencePattern);
    private sealed record FileReadResult(bool IsSuccess, string Content, string ErrorCode)
    {
        public static FileReadResult Success(string content) => new(true, content, string.Empty);
        public static FileReadResult Failure(string errorCode) => new(false, string.Empty, errorCode);
    }
}

public sealed record BuildCorrectionEvidenceResult(
    string Content, string ErrorCode, int UniqueCount, int IncludedCount,
    int DuplicateCount, int DiagnosticsCharacters, int RelatedDeclarationsCharacters)
{
    public bool IsSuccess => string.IsNullOrEmpty(ErrorCode);

    public static BuildCorrectionEvidenceResult Success(
        string content, int uniqueCount, int includedCount, int duplicateCount,
        int diagnosticsCharacters, int relatedDeclarationsCharacters) =>
        new(content, string.Empty, uniqueCount, includedCount, duplicateCount,
            diagnosticsCharacters, relatedDeclarationsCharacters);

    public static BuildCorrectionEvidenceResult Failure(string errorCode) =>
        new(string.Empty, errorCode, 0, 0, 0, 0, 0);
}

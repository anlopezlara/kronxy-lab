using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using Kronxy.Context.Models;

namespace Kronxy.Context.Redaction;

public sealed class ContentRedactor : IContentRedactor
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly IRedactionRuleSource rules;
    private readonly IRedactionValidator validator;

    public ContentRedactor() : this(new GlobalRedactionRules(), new RedactionValidator()) { }

    internal ContentRedactor(IRedactionRuleSource rules, IRedactionValidator validator)
    {
        this.rules = rules;
        this.validator = validator;
    }

    public RedactionResult Redact(RedactionRequest request)
    {
        if (!IsRequestValid(request)) return Failure(RedactionStatus.InternalError);
        int inputBytes;
        try { inputBytes = StrictUtf8.GetByteCount(request.Content); }
        catch (EncoderFallbackException) { return Failure(RedactionStatus.UnprocessableFormat); }
        if (inputBytes > request.Limits.MaxInputBytes) return Failure(RedactionStatus.InputLimitExceeded);
        if (!CanProcessFormat(request.LogicalPath, request.Content)) return Failure(RedactionStatus.UnprocessableFormat);

        try
        {
            var found = rules.Find(request.LogicalPath, request.Content, request.Limits);
            if (found.Count > request.Limits.MaxRedactionsPerFile) return Failure(RedactionStatus.TooManyRedactions);
            var selected = ResolveOverlaps(found);
            if (selected.Count > request.Limits.MaxRedactionsPerFile) return Failure(RedactionStatus.TooManyRedactions);
            var output = Apply(request.Content, selected);
            if (StrictUtf8.GetByteCount(output) > request.Limits.MaxOutputBytes) return Failure(RedactionStatus.OutputLimitExceeded);

            var validation = validator.Validate(request.LogicalPath, output, request.Limits);
            if (!validation.IsSafe)
                return new RedactionResult { Status = RedactionStatus.SensitiveContentRemaining, Findings = validation.Findings };

            var records = selected.GroupBy(match => new { match.LineNumber, match.RuleId, match.Category })
                .Select(group => new RedactionRecord
                {
                    RelativePath = RedactionMetadata.SafePath, LineNumber = group.Key.LineNumber,
                    RuleId = group.Key.RuleId, Category = group.Key.Category.ToString(), MatchCount = group.Count()
                })
                .OrderBy(record => record.RelativePath, StringComparer.Ordinal)
                .ThenBy(record => record.LineNumber)
                .ThenBy(record => record.RuleId, StringComparer.Ordinal)
                .ThenBy(record => record.Category, StringComparer.Ordinal)
                .ToArray();
            return new RedactionResult
            {
                Status = records.Length == 0 ? RedactionStatus.SuccessUnchanged : RedactionStatus.SuccessRedacted,
                Content = output,
                Records = records
            };
        }
        catch (RegexMatchTimeoutException) { return Failure(RedactionStatus.RuleTimeout); }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Failure(RedactionStatus.InternalError);
        }
    }

    private static bool IsRequestValid(RedactionRequest? request) => request is not null &&
        !string.IsNullOrWhiteSpace(request.LogicalPath) && request.LogicalPath.IndexOfAny(['\0', '\r', '\n']) < 0 &&
        request.Content is not null && request.Limits is not null && request.Limits.IsValid();

    private static bool CanProcessFormat(string path, string content)
    {
        var extension = Path.GetExtension(path);
        try
        {
            if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                using var _ = JsonDocument.Parse(content, new JsonDocumentOptions { MaxDepth = 64, CommentHandling = JsonCommentHandling.Disallow });
            }
            else if (extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
                     extension.Equals(".config", StringComparison.OrdinalIgnoreCase) ||
                     extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = XmlReader.Create(new StringReader(content), new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1_048_576
                });
                while (reader.Read()) { }
            }
            else if (extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
                     extension.Equals(".yml", StringComparison.OrdinalIgnoreCase))
            {
                var unsupported = GlobalRedactionRules.CreateRegex(
                    "(?m)(?:^|[ \\t])(?:[&*!][A-Za-z0-9_-]+)", RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(250));
                if (unsupported.IsMatch(content)) return false;
            }
            return true;
        }
        catch (Exception exception) when (exception is JsonException or XmlException) { return false; }
    }

    private static IReadOnlyList<RedactionMatch> ResolveOverlaps(IReadOnlyList<RedactionMatch> matches)
    {
        var selected = new List<RedactionMatch>();
        foreach (var candidate in matches.OrderBy(match => match.Priority).ThenBy(match => match.Start).ThenByDescending(match => match.Length))
        {
            if (candidate.Start < 0 || candidate.Length <= 0) continue;
            var end = checked(candidate.Start + candidate.Length);
            if (selected.Any(existing => candidate.Start < existing.Start + existing.Length && end > existing.Start)) continue;
            selected.Add(candidate);
        }
        return selected.OrderBy(match => match.Start).ToArray();
    }

    private static string Apply(string content, IReadOnlyList<RedactionMatch> matches)
    {
        var builder = new StringBuilder(content);
        foreach (var match in matches.OrderByDescending(match => match.Start))
            builder.Remove(match.Start, match.Length).Insert(match.Start, match.Replacement);
        return builder.ToString();
    }

    private static RedactionResult Failure(RedactionStatus status) => new() { Status = status, Content = null };
}

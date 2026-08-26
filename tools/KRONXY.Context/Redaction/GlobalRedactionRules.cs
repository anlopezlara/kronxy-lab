using System.Text.RegularExpressions;

namespace Kronxy.Context.Redaction;

internal sealed class GlobalRedactionRules : IRedactionRuleSource
{
    public IReadOnlyList<RedactionMatch> Find(string logicalPath, string content, RedactionLimits limits)
    {
        var matches = new List<RedactionMatch>();
        var lineStarts = BuildLineStarts(content);
        matches.AddRange(StructuredAssignmentScanner.Find(logicalPath, content));
        AddRegex(matches, content,
            "(?m)^[ \\t]*(?:private[_ .-]?key|secret|client[_ .-]?secret|shared[_ .-]?secret|credential|token|access[_ .-]?token|refresh[_ .-]?token|id[_ .-]?token|bearer[_ .-]?token|personal[_ .-]?access[_ .-]?token|pat|password|passwd|pwd|passphrase|api[_ .-]?key|subscription[_ .-]?key|x[_ .-]?api[_ .-]?key|account[_ .-]?key|shared[_ .-]?access[_ .-]?key|secret[_ .-]?access[_ .-]?key|connection[_ .-]?strings?)\\s*:\\s*[|>][+-]?[^\\r\\n]*(?:\\r\\n|\\r|\\n)(?<value>(?:[ \\t]+[^\\r\\n]*(?:\\r\\n|\\r|\\n|$))+)",
            "yaml-block-scalar", RedactionCategory.PrivateKey, 20, limits.RegexTimeout,
            value => PreserveLineBreaks(value, RedactionPlaceholders.For(RedactionCategory.PrivateKey)), "value", lineStarts: lineStarts);
        AddRegex(matches, content,
            "-----BEGIN (?:RSA |EC |OPENSSH |ENCRYPTED )?PRIVATE KEY-----[\\s\\S]{0,262144}?-----END (?:RSA |EC |OPENSSH |ENCRYPTED )?PRIVATE KEY-----",
            "private-key-block", RedactionCategory.PrivateKey, 10, limits.RegexTimeout,
            value => RedactPrivateKey(value), lineStarts: lineStarts);
        AddRegex(matches, content,
            "(?im)^(?<name>(?:Proxy-)?Authorization|X-API-Key)\\s*:\\s*(?<value>[^\\r\\n]+)$",
            "authorization-header", RedactionCategory.AuthorizationHeader, 10, limits.RegexTimeout,
            _ => RedactionPlaceholders.For(RedactionCategory.AuthorizationHeader), "value", lineStarts: lineStarts);
        AddRegex(matches, content,
            "(?i)(?:Password|Pwd|User[ ]ID|UID|AccountKey|SharedAccessKey|SharedAccessSignature)\\s*=\\s*(?<value>\"(?:\\\\.|[^\"\\\\])*\"|'[^']*'|[^;\"'\\r\\n]+)",
            "connection-string-credential", RedactionCategory.ConnectionString, 40, limits.RegexTimeout,
            _ => RedactionPlaceholders.For(RedactionCategory.ConnectionString), "value", lineStarts: lineStarts);
        AddUrls(matches, content, limits.RegexTimeout, lineStarts);
        AddRegex(matches, content,
            "(?<![A-Za-z0-9_-])(?:eyJ[A-Za-z0-9_-]{4,}\\.[A-Za-z0-9_-]{4,}\\.[A-Za-z0-9_-]{4,})(?![A-Za-z0-9_-])",
            "jwt", RedactionCategory.Token, 60, limits.RegexTimeout, useNonBacktracking: false, lineStarts: lineStarts);
        AddRegex(matches, content,
            "(?<![A-Za-z0-9])(?:gh[pousr]_[A-Za-z0-9]{20,}|glpat-[A-Za-z0-9_-]{20,}|xox[baprs]-[A-Za-z0-9-]{16,}|sk-[A-Za-z0-9_-]{20,}|AIza[A-Za-z0-9_-]{20,}|sk_(?:live|test)_[A-Za-z0-9]{16,}|AKIA[A-Z0-9]{16})(?![A-Za-z0-9])",
            "known-token-prefix", RedactionCategory.Token, 60, limits.RegexTimeout, useNonBacktracking: false, lineStarts: lineStarts);
        return matches;
    }

    private static void AddUrls(ICollection<RedactionMatch> matches, string content, TimeSpan timeout, int[] lineStarts)
    {
        var regex = CreateRegex("(?i)https?://[^\\s\"'<>]+", RegexOptions.NonBacktracking, timeout);
        foreach (Match match in regex.Matches(content))
        {
            if (!Uri.TryCreate(match.Value, UriKind.Absolute, out var uri)) continue;
            var replacement = SanitizeUri(uri, out var category);
            if (replacement is null) continue;
            matches.Add(new RedactionMatch(match.Index, match.Length, category == RedactionCategory.UrlCredential
                ? "url-credential" : "signed-url", category, replacement, 50, LineOf(lineStarts, match.Index)));
        }
    }

    private static string? SanitizeUri(Uri uri, out RedactionCategory category)
    {
        category = RedactionCategory.SignedUrl;
        var builder = new UriBuilder(uri);
        var changed = false;
        if (!string.IsNullOrEmpty(builder.UserName) || !string.IsNullOrEmpty(builder.Password))
        {
            builder.UserName = RedactionPlaceholders.For(RedactionCategory.UrlCredential);
            builder.Password = string.Empty;
            category = RedactionCategory.UrlCredential;
            changed = true;
        }
        if (builder.Query.Length > 1)
        {
            var parts = builder.Query[1..].Split('&');
            for (var index = 0; index < parts.Length; index++)
            {
                var equals = parts[index].IndexOf('=');
                var name = Uri.UnescapeDataString(equals < 0 ? parts[index] : parts[index][..equals]);
                if (!IsSensitiveQueryName(name)) continue;
                parts[index] = Uri.EscapeDataString(name) + "=" + RedactionPlaceholders.For(RedactionCategory.SignedUrl);
                changed = true;
            }
            builder.Query = string.Join('&', parts);
        }
        return changed ? builder.Uri.AbsoluteUri : null;
    }

    private static bool IsSensitiveQueryName(string name) => name.Equals("token", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("access_token", StringComparison.OrdinalIgnoreCase) || name.Equals("api_key", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("apikey", StringComparison.OrdinalIgnoreCase) || name.Equals("key", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("sig", StringComparison.OrdinalIgnoreCase) || name.Equals("signature", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("client_secret", StringComparison.OrdinalIgnoreCase) || name.Equals("code", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("X-Amz-Signature", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("X-Amz-Credential", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("X-Amz-Security-Token", StringComparison.OrdinalIgnoreCase);

    private static void AddRegex(
        ICollection<RedactionMatch> matches, string content, string pattern, string ruleId,
        RedactionCategory category, int priority, TimeSpan timeout,
        Func<string, string>? replacement = null, string? captureName = null,
        bool useNonBacktracking = true, int[]? lineStarts = null)
    {
        var regex = CreateRegex(pattern,
            useNonBacktracking ? RegexOptions.NonBacktracking : RegexOptions.None, timeout);
        foreach (Match match in regex.Matches(content))
        {
            var target = captureName is null ? match : match.Groups[captureName];
            if (!target.Success || target.Length == 0) continue;
            if (RedactionPlaceholders.IsValid(target.Value.Trim())) continue;
            matches.Add(new RedactionMatch(target.Index, target.Length, ruleId, category,
                replacement?.Invoke(target.Value) ?? RedactionPlaceholders.For(category), priority,
                LineOf(lineStarts ?? BuildLineStarts(content), target.Index)));
        }
    }

    internal static Regex CreateRegex(string pattern, RegexOptions options, TimeSpan timeout)
    {
        var effectiveOptions = options | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
        try
        {
            return new Regex(pattern, effectiveOptions, timeout);
        }
        catch (NotSupportedException) when (effectiveOptions.HasFlag(RegexOptions.NonBacktracking))
        {
            return new Regex(pattern, effectiveOptions & ~RegexOptions.NonBacktracking, timeout);
        }
    }

    private static int[] BuildLineStarts(string content)
    {
        var starts = new List<int> { 0 };
        for (var index = 0; index < content.Length; index++) if (content[index] == '\n') starts.Add(index + 1);
        return starts.ToArray();
    }

    private static int LineOf(int[] starts, int index)
    {
        var position = Array.BinarySearch(starts, index);
        return position >= 0 ? position + 1 : ~position;
    }

    private static string RedactPrivateKey(string value)
    {
        var first = value.IndexOfAny(['\r', '\n']);
        var last = value.LastIndexOfAny(['\r', '\n']);
        if (first < 0 || last <= first) return RedactionPlaceholders.For(RedactionCategory.PrivateKey);
        var header = value[..first];
        var footerStart = last + 1;
        if (value[last] == '\n' && last > 0 && value[last - 1] == '\r') last--;
        var firstBreakLength = value[first] == '\r' && first + 1 < value.Length && value[first + 1] == '\n' ? 2 : 1;
        var firstBreak = value.Substring(first, firstBreakLength);
        var middle = value.Substring(first + firstBreakLength, last - first - firstBreakLength);
        var preservedBreaks = string.Concat(Regex.Matches(middle, "\\r\\n|\\r|\\n", RegexOptions.NonBacktracking,
            TimeSpan.FromMilliseconds(100)).Select(match => match.Value));
        var lastBreak = value.Substring(last, footerStart - last);
        return header + firstBreak + RedactionPlaceholders.For(RedactionCategory.PrivateKey) + preservedBreaks + lastBreak + value[footerStart..];
    }

    private static string PreserveLineBreaks(string value, string placeholder)
    {
        var breaks = Regex.Matches(value, "\\r\\n|\\r|\\n", RegexOptions.NonBacktracking,
            TimeSpan.FromMilliseconds(100)).Select(match => match.Value).ToArray();
        return placeholder + string.Concat(breaks);
    }
}

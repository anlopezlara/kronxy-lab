namespace Kronxy.Context.Redaction;

using System.Text.Json;
using System.Text.RegularExpressions;

internal static class StructuredAssignmentScanner
{
    private static readonly Dictionary<string, RedactionCategory> SensitiveNames = BuildNames();
    private static readonly HashSet<string> AllowedValues = new(StringComparer.OrdinalIgnoreCase)
    { "", "null", "example", "sample", "dummy", "YOUR_VALUE_HERE", "REPLACE_ME", "CHANGE_ME", "{", "[", "|", ">" };

    private static readonly TimeSpan ScanTimeout = TimeSpan.FromMilliseconds(250);

    public static IReadOnlyList<RedactionMatch> Find(string logicalPath, string content)
    {
        var matches = new List<RedactionMatch>();
        var extension = Path.GetExtension(logicalPath);
        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            FindJson(content, matches);
            return matches;
        }
        if (extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".config", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            FindXmlDocument(content, matches);
            return matches;
        }
        var offset = 0;
        var lineNumber = 1;
        var inConnectionStrings = false;
        foreach (var segment in EnumerateLines(content))
        {
            var line = segment.Text;
            var trimmed = line.TrimStart();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed.StartsWith(';') ||
                trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                offset += segment.TotalLength; lineNumber++; continue;
            }

            if (TryFindXml(line, offset, lineNumber, matches))
            {
                offset += segment.TotalLength; lineNumber++; continue;
            }

            var separator = FindAssignmentSeparator(line);
            if (separator >= 0)
            {
                var keyPart = line[..separator].Trim();
                if (keyPart.StartsWith("export ", StringComparison.OrdinalIgnoreCase)) keyPart = keyPart[7..].TrimStart();
                var keyToken = keyPart.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? keyPart;
                var key = NormalizeName(keyToken.Trim('"', '\'', '<', '>'));
                if (key == "connectionstrings") inConnectionStrings = true;
                var category = inConnectionStrings && key != "connectionstrings"
                    ? RedactionCategory.ConnectionString
                    : SensitiveNames.GetValueOrDefault(key);
                if (category != default || key == "password")
                {
                    if (category == default) category = RedactionCategory.Password;
                    AddValueMatch(line, separator + 1, offset, lineNumber, category, "structured-assignment", matches);
                }
            }

            if (inConnectionStrings && trimmed.StartsWith('}')) inConnectionStrings = false;
            offset += segment.TotalLength;
            lineNumber++;
        }
        return matches;
    }

    private static void FindJson(string content, ICollection<RedactionMatch> matches)
    {
        var regex = new Regex("(?<key>\"(?:\\\\.|[^\"\\\\])*\")\\s*:\\s*(?<value>\"(?:\\\\.|[^\"\\\\])*\")",
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, ScanTimeout);
        foreach (Match match in regex.Matches(content))
        {
            var key = JsonSerializer.Deserialize<string>(match.Groups["key"].Value) ?? string.Empty;
            var normalized = NormalizeName(key);
            if (!SensitiveNames.TryGetValue(normalized, out var category)) continue;
            var value = match.Groups["value"];
            var decoded = JsonSerializer.Deserialize<string>(value.Value) ?? string.Empty;
            if (IsAllowed(decoded)) continue;
            matches.Add(new RedactionMatch(value.Index + 1, value.Length - 2, "json-property", category,
                RedactionPlaceholders.For(category), 20, LineOf(content, value.Index)));
        }
    }

    private static void FindXmlDocument(string content, ICollection<RedactionMatch> matches)
    {
        var tagRegex = new Regex("<(?<tag>[A-Za-z_][^\\s/>]*)(?<attrs>[^<>]*?)>",
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, ScanTimeout);
        foreach (Match tag in tagRegex.Matches(content))
        {
            var attrs = tag.Groups["attrs"];
            var attributes = ParseAttributes(attrs.Value, attrs.Index).ToArray();
            var connection = attributes.FirstOrDefault(a => a.Name is not null && NormalizeName(a.Name) == "connectionstring");
            if (connection.Name is not null) AddXml(connection, RedactionCategory.ConnectionString, "xml-connection-string");
            var key = attributes.FirstOrDefault(a => a.Name is not null && NormalizeName(a.Name) == "key");
            var value = attributes.FirstOrDefault(a => a.Name is not null && NormalizeName(a.Name) == "value");
            if (key.Name is not null && value.Name is not null &&
                SensitiveNames.TryGetValue(NormalizeName(key.Value), out var category)) AddXml(value, category, "xml-add-value");
        }

        foreach (var pair in SensitiveNames)
        {
            var element = new Regex($"<(?<name>{Regex.Escape(pair.Key)})\\s*>(?<value>[\\s\\S]*?)</\\k<name>\\s*>",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, ScanTimeout);
            foreach (Match match in element.Matches(content))
            {
                var value = match.Groups["value"];
                if (!IsAllowed(value.Value)) matches.Add(new RedactionMatch(value.Index, value.Length, "xml-element", pair.Value,
                    RedactionPlaceholders.For(pair.Value), 20, LineOf(content, value.Index)));
            }
        }
        return;

        void AddXml((string? Name, string Value, int Start, int Length) attribute, RedactionCategory category, string rule)
        {
            if (!IsAllowed(attribute.Value)) matches.Add(new RedactionMatch(attribute.Start, attribute.Length, rule, category,
                RedactionPlaceholders.For(category), 20, LineOf(content, attribute.Start)));
        }
    }

    private static IEnumerable<(string? Name, string Value, int Start, int Length)> ParseAttributes(string text, int offset)
    {
        var regex = new Regex("(?<name>[A-Za-z_][A-Za-z0-9_.:-]*)\\s*=\\s*(?<quote>[\"'])(?<value>[\\s\\S]*?)\\k<quote>",
            RegexOptions.CultureInvariant, ScanTimeout);
        foreach (Match match in regex.Matches(text))
        {
            var value = match.Groups["value"];
            yield return (match.Groups["name"].Value, value.Value, offset + value.Index, value.Length);
        }
    }

    private static void AddValueMatch(
        string line, int valueStart, int offset, int lineNumber, RedactionCategory category,
        string ruleId, ICollection<RedactionMatch> matches)
    {
        while (valueStart < line.Length && char.IsWhiteSpace(line[valueStart])) valueStart++;
        if (valueStart >= line.Length) return;
        var quote = line[valueStart] is '"' or '\'' ? line[valueStart++] : '\0';
        var end = valueStart;
        var escaped = false;
        while (end < line.Length)
        {
            var current = line[end];
            if (quote != '\0')
            {
                if (current == quote && !escaped) break;
                escaped = current == '\\' && !escaped;
                if (current != '\\') escaped = false;
            }
            else if (current is '#' or ';' or ',' || char.IsWhiteSpace(current) && line[(end + 1)..].TrimStart().StartsWith("//")) break;
            end++;
        }
        var value = line[valueStart..end].TrimEnd();
        if (value.Length == 0 || IsAllowed(value)) return;
        var actualLength = value.Length;
        matches.Add(new RedactionMatch(offset + valueStart, actualLength, ruleId, category,
            RedactionPlaceholders.For(category), 20, lineNumber));
    }

    private static bool TryFindXml(string line, int offset, int lineNumber, ICollection<RedactionMatch> matches)
    {
        var found = false;
        foreach (var pair in SensitiveNames)
        {
            var elementOpen = "<" + pair.Key + ">";
            var start = line.IndexOf(elementOpen, StringComparison.OrdinalIgnoreCase);
            if (start >= 0)
            {
                var valueStart = start + elementOpen.Length;
                var close = line.IndexOf("</", valueStart, StringComparison.Ordinal);
                if (close > valueStart && !IsAllowed(line[valueStart..close]))
                {
                    matches.Add(new RedactionMatch(offset + valueStart, close - valueStart, "xml-element", pair.Value,
                        RedactionPlaceholders.For(pair.Value), 20, lineNumber));
                    found = true;
                }
            }
        }

        var connection = FindAttributeValue(line, "connectionString");
        if (connection is not null &&
            !IsAllowed(line.Substring(connection.Value.Start, connection.Value.Length)))
        {
            matches.Add(new RedactionMatch(offset + connection.Value.Start, connection.Value.Length,
                "xml-connection-string", RedactionCategory.ConnectionString,
                RedactionPlaceholders.For(RedactionCategory.ConnectionString), 20, lineNumber));
            found = true;
        }
        var key = FindAttributeValue(line, "key");
        var value = FindAttributeValue(line, "value");
        if (key is not null && value is not null)
        {
            var keyText = line.Substring(key.Value.Start, key.Value.Length);
            var normalized = NormalizeName(keyText);
            if (SensitiveNames.TryGetValue(normalized, out var category) &&
                !IsAllowed(line.Substring(value.Value.Start, value.Value.Length)))
            {
                matches.Add(new RedactionMatch(offset + value.Value.Start, value.Value.Length, "xml-add-value", category,
                    RedactionPlaceholders.For(category), 20, lineNumber));
                found = true;
            }
        }
        return found;
    }

    private static (int Start, int Length)? FindAttributeValue(string line, string name)
    {
        var marker = name + "=";
        var index = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return null;
        var start = index + marker.Length;
        while (start < line.Length && char.IsWhiteSpace(line[start])) start++;
        if (start >= line.Length || line[start] is not ('"' or '\'')) return null;
        var quote = line[start++];
        var end = line.IndexOf(quote, start);
        return end > start ? (start, end - start) : null;
    }

    private static int FindAssignmentSeparator(string line)
    {
        var inQuote = false; char quote = '\0';
        for (var index = 0; index < line.Length; index++)
        {
            if (line[index] is '"' or '\'') { if (!inQuote) { inQuote = true; quote = line[index]; } else if (quote == line[index]) inQuote = false; }
            if (!inQuote && line[index] is '=' or ':') return index;
        }
        return -1;
    }

    private static bool IsAllowed(string value)
    {
        var trimmed = value.Trim().Trim('"', '\'');
        if (AllowedValues.Contains(trimmed) || RedactionPlaceholders.IsValid(trimmed)) return true;
        return IsWholePlaceholder(trimmed, "${", "}") || IsWholePlaceholder(trimmed, "$(", ")") ||
               IsWholePlaceholder(trimmed, "%", "%") || IsWholePlaceholder(trimmed, "{{", "}}") ||
               IsWholePlaceholder(trimmed, "<", ">") ||
               trimmed.StartsWith("$ENV:", StringComparison.OrdinalIgnoreCase) && trimmed[5..].All(IsVariableCharacter) ||
               trimmed.StartsWith("__", StringComparison.Ordinal) && trimmed.EndsWith("__", StringComparison.Ordinal) &&
               trimmed[2..^2].All(IsVariableCharacter);
    }

    private static bool IsWholePlaceholder(string value, string prefix, string suffix) =>
        value.StartsWith(prefix, StringComparison.Ordinal) && value.EndsWith(suffix, StringComparison.Ordinal) &&
        value[prefix.Length..^suffix.Length].Length > 0 && value[prefix.Length..^suffix.Length].All(IsVariableCharacter);
    private static bool IsVariableCharacter(char value) => char.IsAsciiLetterOrDigit(value) || value == '_';
    private static string NormalizeName(string value) => new(value.Where(char.IsAsciiLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static int LineOf(string content, int index)
    {
        var line = 1;
        for (var current = 0; current < index; current++) if (content[current] == '\n') line++;
        return line;
    }

    private static Dictionary<string, RedactionCategory> BuildNames()
    {
        var result = new Dictionary<string, RedactionCategory>(StringComparer.OrdinalIgnoreCase);
        Add(RedactionCategory.Password, "password", "passwd", "pwd", "passphrase");
        Add(RedactionCategory.GenericSecret, "secret", "clientSecret", "sharedSecret", "credential");
        Add(RedactionCategory.Token, "token", "accessToken", "refreshToken", "idToken", "bearerToken", "personalAccessToken", "pat");
        Add(RedactionCategory.ApiKey, "apiKey", "subscriptionKey", "xApiKey");
        Add(RedactionCategory.CloudCredential, "privateKey", "accountKey", "sharedAccessKey", "secretAccessKey");
        Add(RedactionCategory.ConnectionString, "connectionString", "connectionStrings");
        return result;
        void Add(RedactionCategory category, params string[] names)
        { foreach (var name in names) result[NormalizeName(name)] = category; }
    }

    private static IEnumerable<(string Text, int TotalLength)> EnumerateLines(string content)
    {
        var start = 0;
        while (start < content.Length)
        {
            var newline = content.IndexOf('\n', start);
            if (newline < 0) { yield return (content[start..], content.Length - start); yield break; }
            var textLength = newline - start;
            if (textLength > 0 && content[newline - 1] == '\r') textLength--;
            yield return (content.Substring(start, textLength), newline - start + 1);
            start = newline + 1;
        }
        if (content.Length == 0) yield break;
    }
}

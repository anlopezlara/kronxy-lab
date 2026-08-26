using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;

namespace Kronxy.Context.Redaction;

public sealed class RedactionValidator : IRedactionValidator
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly HashSet<string> SensitiveNames = BuildSensitiveNames();

    public RedactionValidationResult Validate(string logicalPath, string redactedContent, RedactionLimits limits)
    {
        ArgumentNullException.ThrowIfNull(redactedContent);
        var findings = new List<RedactionFinding>();
        var unsafeDetected = false;
        var lineStarts = BuildLineStarts(redactedContent);

        if (limits is null || !limits.IsValid())
            return Unsafe("validator-limits", RedactionCategory.GenericSecret, "Los límites de validación no son seguros.");

        try
        {
            var bytes = StrictUtf8.GetByteCount(redactedContent);
            if (bytes > limits.MaxInputBytes || bytes > limits.MaxOutputBytes)
                return Unsafe("validator-size-limit", RedactionCategory.GenericSecret, "El contenido excede los límites de validación.");

            ValidateStructured(logicalPath, redactedContent);
            ValidateHeaders();
            ValidateConnectionStrings();
            Check("(?<![A-Za-z0-9_-])eyJ[A-Za-z0-9_-]{4,}\\.[A-Za-z0-9_-]{4,}\\.[A-Za-z0-9_-]{4,}(?![A-Za-z0-9_-])",
                "validator-jwt", RedactionCategory.Token, "Quedó un token JWT.", false);
            Check("(?<![A-Za-z0-9])(?:gh[pousr]_[A-Za-z0-9]{20,}|glpat-[A-Za-z0-9_-]{20,}|xox[baprs]-[A-Za-z0-9-]{16,}|sk-[A-Za-z0-9_-]{20,}|AIza[A-Za-z0-9_-]{20,}|AKIA[A-Z0-9]{16})(?![A-Za-z0-9])",
                "validator-known-token", RedactionCategory.Token, "Quedó un token de prefijo conocido.", false);
            Check("(?i)https?://(?!__KRONXY_REDACTED_URL_CREDENTIAL__@)[^\\s\"'<>]*(?:@|[?&](?:token|access_token|api_key|apikey|key|sig|signature|client_secret|code|X-Amz-Signature|X-Amz-Credential|X-Amz-Security-Token)=)(?!__KRONXY_REDACTED_)[^\\s\"'<>]*",
                "validator-url", RedactionCategory.SignedUrl, "Quedó una credencial o firma en URL.", false);
            ValidatePrivateKeys();
            ValidatePlaceholders();

            return new RedactionValidationResult { IsSafe = !unsafeDetected, Findings = findings };
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Unsafe("validator-internal-" + exception.GetType().Name, RedactionCategory.GenericSecret,
                "La validación no pudo completarse de forma segura.");
        }

        void ValidateStructured(string path, string content)
        {
            var extension = Path.GetExtension(path);
            if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase)) ValidateJson(content);
            else if (extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
                     extension.Equals(".config", StringComparison.OrdinalIgnoreCase) ||
                     extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)) ValidateXml(content);
            else
            {
                if (extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase))
                    ValidateYamlBlocks(content);
                ValidateAssignments(content);
            }
        }

        void ValidateJson(string content)
        {
            using var document = JsonDocument.Parse(content, new JsonDocumentOptions { MaxDepth = 64, CommentHandling = JsonCommentHandling.Disallow });
            Visit(document.RootElement, null);
            void Visit(JsonElement element, string? propertyName)
            {
                if (propertyName is not null && IsSensitive(propertyName) && element.ValueKind == JsonValueKind.String &&
                    !IsSafeValue(element.GetString())) Add("validator-json-property", CategoryFor(propertyName), 1, "Quedó una propiedad JSON sensible.");
                if (element.ValueKind == JsonValueKind.Object)
                    foreach (var property in element.EnumerateObject()) Visit(property.Value, property.Name);
                else if (element.ValueKind == JsonValueKind.Array)
                    foreach (var item in element.EnumerateArray()) Visit(item, propertyName);
            }
        }

        void ValidateXml(string content)
        {
            using var reader = XmlReader.Create(new StringReader(content), new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = limits.MaxInputBytes });
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element) continue;
                if (IsSensitive(reader.LocalName) && !reader.IsEmptyElement)
                {
                    var value = reader.ReadElementContentAsString();
                    if (!IsSafeValue(value)) Add("validator-xml-element", CategoryFor(reader.LocalName), 1, "Quedó un elemento XML sensible.");
                    continue;
                }
                var connection = reader.GetAttribute("connectionString") ?? reader.GetAttribute("connectionstring");
                if (connection is not null && !IsSafeValue(connection)) Add("validator-xml-connection-string", RedactionCategory.ConnectionString, 1, "Quedó una cadena de conexión XML.");
                var key = reader.GetAttribute("key");
                var valueAttribute = reader.GetAttribute("value");
                if (key is not null && valueAttribute is not null && IsSensitive(key) && !IsSafeValue(valueAttribute))
                    Add("validator-xml-value", CategoryFor(key), 1, "Quedó un atributo XML sensible.");
            }
        }

        void ValidateAssignments(string content)
        {
            var regex = GlobalRedactionRules.CreateRegex("(?m)^[ \\t]*(?:export[ \\t]+)?(?<name>[A-Za-z_][A-Za-z0-9_. -]*)\\s*[:=]\\s*(?<value>[^\\r\\n]+)$",
                RegexOptions.NonBacktracking, limits.RegexTimeout);
            foreach (Match match in regex.Matches(content))
            {
                var name = match.Groups["name"].Value;
                var value = TrimAssignedValue(match.Groups["value"].Value);
                if (IsSensitive(name) && !IsSafeValue(value) && !IsDeclarativePlaceholder(value))
                    Add("validator-assignment", CategoryFor(name), LineAt(match.Index), "Quedó una asignación sensible.");
            }
        }

        void ValidateYamlBlocks(string content)
        {
            var regex = GlobalRedactionRules.CreateRegex("(?m)^[ \\t]*(?<name>[A-Za-z_][A-Za-z0-9_. -]*)\\s*:\\s*[|>][+-]?[^\\r\\n]*(?:\\r\\n|\\r|\\n)(?<value>(?:[ \\t]+[^\\r\\n]*(?:\\r\\n|\\r|\\n|$))+)",
                RegexOptions.None, limits.RegexTimeout);
            foreach (Match match in regex.Matches(content))
            {
                var name = match.Groups["name"].Value;
                var body = match.Groups["value"].Value.Trim();
                if (IsSensitive(name) && !RedactionPlaceholders.IsValid(body))
                    Add("validator-yaml-block", CategoryFor(name), LineAt(match.Index), "Quedó un escalar YAML sensible.");
            }
        }

        void ValidateHeaders()
        {
            var regex = GlobalRedactionRules.CreateRegex("(?m)^(?<name>(?:Proxy-)?Authorization|X-API-Key)\\s*:\\s*(?<value>[^\\r\\n]+)$",
                RegexOptions.NonBacktracking, limits.RegexTimeout);
            foreach (Match match in regex.Matches(redactedContent))
                if (!RedactionPlaceholders.IsValid(match.Groups["value"].Value.Trim()))
                    Add("validator-header", RedactionCategory.AuthorizationHeader, LineAt(match.Index), "Quedó un encabezado sensible.");
        }

        void ValidateConnectionStrings()
        {
            var regex = GlobalRedactionRules.CreateRegex("(?:Password|Pwd|User[ ]ID|UID|AccountKey|SharedAccessKey|SharedAccessSignature)\\s*=\\s*(?<value>\"(?:\\\\.|[^\"\\\\])*\"|'[^']*'|[^;\"'\\r\\n]+)",
                RegexOptions.NonBacktracking, limits.RegexTimeout);
            foreach (Match match in regex.Matches(redactedContent))
                if (!RedactionPlaceholders.IsValid(match.Groups["value"].Value.Trim().Trim('"', '\'')))
                    Add("validator-connection-string", RedactionCategory.ConnectionString, LineAt(match.Index), "Quedó una credencial de cadena de conexión.");
        }

        void ValidatePrivateKeys()
        {
            var begin = GlobalRedactionRules.CreateRegex("-----BEGIN (?:RSA |EC |OPENSSH |ENCRYPTED )?PRIVATE KEY-----",
                RegexOptions.NonBacktracking, limits.RegexTimeout);
            var safeBlock = GlobalRedactionRules.CreateRegex("\\A-----BEGIN (?:RSA |EC |OPENSSH |ENCRYPTED )?PRIVATE KEY-----\\r?\\n__KRONXY_REDACTED_PRIVATE_KEY__(?:\\r?\\n)*-----END (?:RSA |EC |OPENSSH |ENCRYPTED )?PRIVATE KEY-----",
                RegexOptions.NonBacktracking, limits.RegexTimeout);
            foreach (Match match in begin.Matches(redactedContent))
                if (!safeBlock.IsMatch(redactedContent[match.Index..])) Add("validator-private-key", RedactionCategory.PrivateKey,
                    LineAt(match.Index), "Quedó material de clave privada.");
        }

        void ValidatePlaceholders()
        {
            var regex = GlobalRedactionRules.CreateRegex("[A-Za-z0-9_]*__KRONXY_REDACTED_[A-Za-z0-9_]*",
                RegexOptions.NonBacktracking, limits.RegexTimeout);
            foreach (Match match in regex.Matches(redactedContent))
                if (!RedactionPlaceholders.IsValid(match.Value)) Add("validator-placeholder", RedactionCategory.GenericSecret,
                    LineAt(match.Index), "Se encontró un placeholder de redacción no autorizado.");
        }

        void Check(string pattern, string ruleId, RedactionCategory category, string description, bool nonBacktracking = true)
        {
            var regex = GlobalRedactionRules.CreateRegex(pattern, nonBacktracking ? RegexOptions.NonBacktracking : RegexOptions.None, limits.RegexTimeout);
            foreach (Match match in regex.Matches(redactedContent)) Add(ruleId, category, LineAt(match.Index), description);
        }

        void Add(string ruleId, RedactionCategory category, int line, string description)
        {
            unsafeDetected = true;
            if (findings.Count >= limits.MaxFindingsPerFile) return;
            findings.Add(new RedactionFinding { LogicalPath = RedactionMetadata.SafePath, RuleId = ruleId, Category = category,
                LineNumber = line, Severity = RedactionSeverity.Error, Description = description });
        }

        RedactionValidationResult Unsafe(string ruleId, RedactionCategory category, string description) => new()
        {
            IsSafe = false,
            Findings = [new RedactionFinding { LogicalPath = RedactionMetadata.SafePath, RuleId = ruleId, Category = category,
                LineNumber = 0, Severity = RedactionSeverity.Error, Description = description }]
        };

        int LineAt(int index)
        {
            var position = Array.BinarySearch(lineStarts, index);
            return position >= 0 ? position + 1 : ~position;
        }
    }

    private static bool IsSafeValue(string? value) => string.IsNullOrWhiteSpace(value) ||
        RedactionPlaceholders.IsValid(value.Trim().Trim('"', '\'')) ||
        value.Trim().Equals("null", StringComparison.OrdinalIgnoreCase) ||
        value.Trim().Equals("example", StringComparison.OrdinalIgnoreCase) ||
        value.Trim().Equals("sample", StringComparison.OrdinalIgnoreCase) ||
        value.Trim() is "|" or "|-" or "|+" or ">" or ">-" or ">+";

    private static string TrimAssignedValue(string value)
    {
        var trimmed = value.Trim();
        var comment = trimmed.IndexOfAny(['#', ';']);
        if (comment >= 0) trimmed = trimmed[..comment].TrimEnd();
        return trimmed.Trim('"', '\'');
    }

    private static bool IsDeclarativePlaceholder(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Equals("YOUR_VALUE_HERE", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("REPLACE_ME", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("CHANGE_ME", StringComparison.OrdinalIgnoreCase)) return true;
        return IsWrapped("${", "}") || IsWrapped("$(", ")") || IsWrapped("%", "%") ||
               IsWrapped("{{", "}}") || IsWrapped("<", ">") || IsWrapped("__", "__") ||
               trimmed.StartsWith("$ENV:", StringComparison.OrdinalIgnoreCase) && trimmed[5..].All(IsVariable);
        bool IsWrapped(string prefix, string suffix) => trimmed.StartsWith(prefix, StringComparison.Ordinal) &&
            trimmed.EndsWith(suffix, StringComparison.Ordinal) && trimmed[prefix.Length..^suffix.Length].Length > 0 &&
            trimmed[prefix.Length..^suffix.Length].All(IsVariable);
        static bool IsVariable(char character) => char.IsAsciiLetterOrDigit(character) || character == '_';
    }

    private static bool IsSensitive(string value) => SensitiveNames.Contains(Normalize(value));
    private static RedactionCategory CategoryFor(string value)
    {
        var name = Normalize(value);
        if (name.Contains("password") || name is "pwd" or "passwd" or "passphrase") return RedactionCategory.Password;
        if (name.Contains("apikey") || name.Contains("subscriptionkey")) return RedactionCategory.ApiKey;
        if (name.Contains("connectionstring")) return RedactionCategory.ConnectionString;
        if (name.Contains("privatekey") || name.Contains("accountkey") || name.Contains("accesskey")) return RedactionCategory.CloudCredential;
        if (name.Contains("token") || name == "pat") return RedactionCategory.Token;
        return RedactionCategory.GenericSecret;
    }

    private static HashSet<string> BuildSensitiveNames()
    {
        string[] names = ["password", "passwd", "pwd", "passphrase", "secret", "clientSecret", "sharedSecret", "credential",
            "token", "accessToken", "refreshToken", "idToken", "bearerToken", "personalAccessToken", "pat", "apiKey",
            "subscriptionKey", "xApiKey", "privateKey", "accountKey", "sharedAccessKey", "secretAccessKey",
            "connectionString", "connectionStrings"];
        return names.Select(Normalize).ToHashSet(StringComparer.Ordinal);
    }

    private static string Normalize(string value) => new(value.Where(char.IsAsciiLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static int[] BuildLineStarts(string content)
    {
        var starts = new List<int> { 0 };
        for (var index = 0; index < content.Length; index++) if (content[index] == '\n') starts.Add(index + 1);
        return starts.ToArray();
    }
}

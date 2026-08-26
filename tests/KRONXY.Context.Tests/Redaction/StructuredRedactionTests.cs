using Kronxy.Context.Redaction;
using Xunit;

namespace Kronxy.Context.Tests.Redaction;

public sealed class StructuredRedactionTests
{
    private readonly ContentRedactor redactor = new();

    [Theory]
    [InlineData("settings.json", "{\n  \"password\": \"synthetic-value\"\n}", "__KRONXY_REDACTED_PASSWORD__")]
    [InlineData("settings.json", "{\n  \"ConnectionStrings\": {\n    \"Default\": \"Host=local;Pwd=synthetic\"\n  }\n}", "__KRONXY_REDACTED_CONNECTION_STRING__")]
    [InlineData("app.config", "<configuration><password>synthetic</password></configuration>", "__KRONXY_REDACTED_PASSWORD__")]
    [InlineData("app.config", "<configuration><add key=\"ApiKey\" value=\"synthetic\" /></configuration>", "__KRONXY_REDACTED_API_KEY__")]
    [InlineData("app.config", "<configuration><add name=\"Default\" connectionString=\"Host=local;Pwd=synthetic\" /></configuration>", "__KRONXY_REDACTED_CONNECTION_STRING__")]
    [InlineData("config.yaml", "password: synthetic # retained", "__KRONXY_REDACTED_PASSWORD__")]
    [InlineData("config.env", "export API_KEY=\"synthetic\"", "__KRONXY_REDACTED_API_KEY__")]
    [InlineData("config.ini", "token: synthetic", "__KRONXY_REDACTED_TOKEN__")]
    [InlineData("source.cs", "const string ApiKey = \"synthetic\";", "__KRONXY_REDACTED_API_KEY__")]
    [InlineData("script.sql", "PASSWORD = 'synthetic';", "__KRONXY_REDACTED_PASSWORD__")]
    public void StructuredFormatsRedactAssignedValues(string path, string input, string placeholder)
    {
        var result = redactor.Redact(Request(path, input));
        Assert.True(result.IsSuccess, result.Status + ":" + string.Join(',', result.Findings.Select(finding => finding.RuleId)));
        Assert.Equal(RedactionStatus.SuccessRedacted, result.Status);
        Assert.Contains(placeholder, result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void YamlBlockScalarRemovesBodyAndPreservesLineBreakCount()
    {
        const string input = "private_key: |\r\n  synthetic-line-one\r\n  synthetic-line-two\r\nnext: safe\r\n";
        var result = redactor.Redact(Request("config.yaml", input));
        Assert.True(result.IsSuccess, result.Status + ":" + string.Join(',', result.Findings.Select(finding => finding.RuleId)));
        var content = Assert.IsType<string>(result.Content);
        Assert.DoesNotContain("synthetic-line", content, StringComparison.Ordinal);
        Assert.Equal(input.Count(character => character == '\n'), content.Count(character => character == '\n'));
        Assert.DoesNotContain("\n", content.Replace("\r\n", string.Empty), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\n  \"p\\u0061ssword\" : \"synthetic-value\"\n}")]
    [InlineData("{\n  \"password\"\n  :\n  \"synthetic-value\"\n}")]
    public void JsonEscapedOrMultilineSensitivePropertyCannotBypassRedaction(string input)
    {
        var result = redactor.Redact(Request("settings.json", input));
        Assert.True(result.IsSuccess, result.Status + ":" + string.Join(',', result.Findings.Select(finding => finding.RuleId)));
        Assert.DoesNotContain("synthetic-value", result.Content, StringComparison.Ordinal);
        Assert.Contains("__KRONXY_REDACTED_PASSWORD__", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void XmlAttributesWithWhitespaceAndNewlinesAreRedacted()
    {
        const string input = "<configuration><add key = \"ApiKey\"\n value = \"synthetic-value\" /></configuration>";
        var result = redactor.Redact(Request("app.config", input));
        Assert.True(result.IsSuccess, result.Status + ":" + string.Join(',', result.Findings.Select(finding => finding.RuleId)));
        Assert.DoesNotContain("synthetic-value", result.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("api_key: |\n  synthetic-one\n  synthetic-two\nnext: safe\n")]
    [InlineData("secret: >-\r\n  synthetic-one\r\n  synthetic-two\r\nnext: safe\r\n")]
    public void EverySensitiveYamlBlockScalarIsRedacted(string input)
    {
        var result = redactor.Redact(Request("config.yaml", input));
        Assert.True(result.IsSuccess, result.Status + ":" + string.Join(',', result.Findings.Select(finding => finding.RuleId)));
        Assert.DoesNotContain("synthetic-", result.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("password: ${PASSWORD}")]
    [InlineData("password: $(PASSWORD)")]
    [InlineData("password: %PASSWORD%")]
    [InlineData("password: $ENV:PASSWORD")]
    [InlineData("password: {{PASSWORD}}")]
    [InlineData("password: __PASSWORD__")]
    [InlineData("password: <PASSWORD>")]
    [InlineData("password: YOUR_VALUE_HERE")]
    [InlineData("password: example")]
    public void DeclarativePlaceholdersAreNotRedacted(string input)
    {
        var result = redactor.Redact(Request("config.yaml", input));
        Assert.Equal(RedactionStatus.SuccessUnchanged, result.Status);
        Assert.Equal(input, result.Content);
        Assert.Empty(result.Records);
    }

    [Theory]
    [InlineData("PasswordValidator validates password policies.")]
    [InlineData("TokenType is an enum.")]
    [InlineData("ApiKeyOptions configures names only.")]
    [InlineData("nameof(Password)")]
    [InlineData("publicKey = sample")]
    [InlineData("https://example.invalid/path?q=safe")]
    [InlineData("user@example.invalid")]
    [InlineData("2026-08-26")]
    [InlineData("1.2.3")]
    [InlineData("0123456789012345678901234567890123456789")]
    public void ImportantFalsePositivesRemainUnchanged(string input)
    {
        var result = redactor.Redact(Request("README.md", input));
        Assert.Equal(RedactionStatus.SuccessUnchanged, result.Status);
        Assert.Equal(input, result.Content);
    }

    [Fact]
    public void EmptyInputIsValid() => Assert.Equal(RedactionStatus.SuccessUnchanged,
        redactor.Redact(Request("empty.txt", string.Empty)).Status);

    [Theory]
    [InlineData("bad.json", "{\"password\":")]
    [InlineData("bad.xml", "<!DOCTYPE x [<!ENTITY y SYSTEM 'file:///tmp/x'>]><x>&y;</x>")]
    public void InvalidOrUnsafeStructuredFormatFailsWithoutContent(string path, string input)
    {
        var result = redactor.Redact(Request(path, input));
        Assert.Equal(RedactionStatus.UnprocessableFormat, result.Status);
        Assert.Null(result.Content);
    }

    [Theory]
    [InlineData("secret_value: &credential synthetic\npassword: *credential\n")]
    [InlineData("password: !custom synthetic\n")]
    public void UnsupportedYamlConstructsFailClosed(string input)
    {
        var result = redactor.Redact(Request("unsafe.yaml", input));
        Assert.Equal(RedactionStatus.UnprocessableFormat, result.Status);
        Assert.Null(result.Content);
    }

    internal static RedactionRequest Request(string path, string content, RedactionLimits? limits = null) => new()
    { LogicalPath = path, Content = content, Limits = limits ?? new RedactionLimits() };
}

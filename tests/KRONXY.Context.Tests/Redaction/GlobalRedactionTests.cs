using Kronxy.Context.Redaction;
using Xunit;

namespace Kronxy.Context.Tests.Redaction;

public sealed class GlobalRedactionTests
{
    private readonly ContentRedactor redactor = new();

    [Theory]
    [InlineData("Authorization: Bearer synthetic-bearer-value", "__KRONXY_REDACTED_AUTHORIZATION_HEADER__")]
    [InlineData("Authorization: Basic synthetic-basic-value", "__KRONXY_REDACTED_AUTHORIZATION_HEADER__")]
    [InlineData("X-API-Key: synthetic-api-value", "__KRONXY_REDACTED_AUTHORIZATION_HEADER__")]
    [InlineData("Server=local;User ID=synthetic-user;Password=synthetic-password;Database=test", "__KRONXY_REDACTED_CONNECTION_STRING__")]
    public void HighConfidenceGlobalRulesRedact(string input, string placeholder)
    {
        var result = redactor.Redact(StructuredRedactionTests.Request("plain.txt", input));
        Assert.True(result.IsSuccess);
        Assert.Contains(placeholder, result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void KnownTokenAndJwtAreBuiltAtRuntimeAndRedacted()
    {
        var known = string.Concat("gh", "p_", new string('a', 24));
        var jwt = string.Join('.', "eyJ" + new string('a', 8), new string('b', 8), new string('c', 8));
        var result = redactor.Redact(StructuredRedactionTests.Request("plain.txt", known + "\n" + jwt));
        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(known, result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(jwt, result.Content, StringComparison.Ordinal);
        Assert.Equal(2, result.Records.Sum(record => record.MatchCount));
    }

    [Fact]
    public void PrivateKeyBodyIsConstructedAtRuntimeRemovedAndLineCountPreserved()
    {
        var body = string.Concat(new string('A', 32), "\n", new string('B', 32));
        var input = "-----BEGIN " + "PRIVATE KEY-----\n" + body + "\n-----END " + "PRIVATE KEY-----";
        var result = redactor.Redact(StructuredRedactionTests.Request("key.txt", input));
        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(body, result.Content, StringComparison.Ordinal);
        Assert.Contains("__KRONXY_REDACTED_PRIVATE_KEY__", result.Content, StringComparison.Ordinal);
        Assert.Equal(input.Count(character => character == '\n'), result.Content!.Count(character => character == '\n'));
    }

    [Fact]
    public void PublicKeyIsNotRedacted()
    {
        var input = "-----BEGIN " + "PUBLIC KEY-----\n" + new string('A', 40) + "\n-----END " + "PUBLIC KEY-----";
        Assert.Equal(input, redactor.Redact(StructuredRedactionTests.Request("public.txt", input)).Content);
    }

    [Theory]
    [InlineData("https://synthetic-user:synthetic-password@example.invalid/path?q=safe", "__KRONXY_REDACTED_URL_CREDENTIAL__")]
    [InlineData("https://example.invalid/path?safe=yes&X-Amz-Signature=synthetic-signature", "__KRONXY_REDACTED_SIGNED_URL_VALUE__")]
    [InlineData("https://example.invalid/path?token=synthetic-token&safe=yes", "__KRONXY_REDACTED_SIGNED_URL_VALUE__")]
    public void UrlCredentialsAndSignedParametersAreSanitized(string input, string placeholder)
    {
        var result = redactor.Redact(StructuredRedactionTests.Request("url.txt", input));
        Assert.True(result.IsSuccess);
        Assert.Contains(placeholder, Uri.UnescapeDataString(result.Content!), StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-", result.Content, StringComparison.Ordinal);
        Assert.Contains("example.invalid", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactionIsIdempotent()
    {
        var first = redactor.Redact(StructuredRedactionTests.Request("config.env", "PASSWORD=synthetic"));
        var second = redactor.Redact(StructuredRedactionTests.Request("config.env", first.Content!));
        Assert.Equal(first.Content, second.Content);
        Assert.Equal(RedactionStatus.SuccessUnchanged, second.Status);
        Assert.Empty(second.Records);
    }
}

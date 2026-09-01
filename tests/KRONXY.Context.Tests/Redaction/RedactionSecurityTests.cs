using System.Text.Json;
using System.Text.RegularExpressions;
using Kronxy.Context.Models;
using Kronxy.Context.Redaction;
using Xunit;

namespace Kronxy.Context.Tests.Redaction;

public sealed class RedactionSecurityTests
{
    [Fact]
    public void RequestToStringNeverContainsContent()
    {
        const string synthetic = "synthetic-sensitive-value";
        var request = StructuredRedactionTests.Request("file.txt", synthetic);
        Assert.DoesNotContain(synthetic, request.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(synthetic, JsonSerializer.Serialize(request), StringComparison.Ordinal);
    }

    [Fact]
    public void ResultToStringNeverContainsReturnedContent()
    {
        const string synthetic = "synthetic-safe-content";
        var result = new ContentRedactor().Redact(StructuredRedactionTests.Request("file.txt", synthetic));
        Assert.DoesNotContain(synthetic, result.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void InputAndOutputLimitsAreMeasuredInUtf8Bytes()
    {
        var redactor = new ContentRedactor();
        var exact = redactor.Redact(StructuredRedactionTests.Request("file.txt", "ñ", Limits(input: 2, output: 100)));
        var exceeded = redactor.Redact(StructuredRedactionTests.Request("file.txt", "ñ", Limits(input: 1, output: 100)));
        var growth = redactor.Redact(StructuredRedactionTests.Request("file.env", "PWD=x", Limits(input: 100, output: 5)));
        Assert.True(exact.IsSuccess);
        Assert.Equal(RedactionStatus.InputLimitExceeded, exceeded.Status);
        Assert.Null(exceeded.Content);
        Assert.Equal(RedactionStatus.OutputLimitExceeded, growth.Status);
        Assert.Null(growth.Content);
    }

    [Fact]
    public void ExactRedactionLimitPassesAndOneMoreFailsClosed()
    {
        var redactor = new ContentRedactor();
        var exact = redactor.Redact(StructuredRedactionTests.Request("file.env", "PWD=a\nTOKEN=b", Limits(redactions: 2)));
        var exceeded = redactor.Redact(StructuredRedactionTests.Request("file.env", "PWD=a\nTOKEN=b\nAPI_KEY=c", Limits(redactions: 2)));
        Assert.True(exact.IsSuccess);
        Assert.Equal(RedactionStatus.TooManyRedactions, exceeded.Status);
        Assert.Null(exceeded.Content);
    }

    [Fact]
    public void DeterministicTimeoutSeamFailsWithoutContent()
    {
        var redactor = new ContentRedactor(new TimeoutRules(), new RedactionValidator());
        var result = redactor.Redact(StructuredRedactionTests.Request("file.txt", "safe"));
        Assert.Equal(RedactionStatus.RuleTimeout, result.Status);
        Assert.Null(result.Content);
    }

    [Fact]
    public void IndependentValidatorBlocksRemainingSecretAndMalformedPlaceholder()
    {
        var validator = new RedactionValidator();
        Assert.False(validator.Validate("file.env", "PASS" + "WORD=synthetic", new RedactionLimits()).IsSafe);
        Assert.False(validator.Validate("file.txt", "__KRONXY_" + "REDACTED_BAD", new RedactionLimits()).IsSafe);
        Assert.True(validator.Validate("file.txt", "__KRONXY_REDACTED_TOKEN__", new RedactionLimits()).IsSafe);
    }

    [Theory]
    [InlineData("X-API-Key: synthetic-value")]
    [InlineData("Server=local;Password=synthetic-value;Database=test")]
    [InlineData("-----BEGIN " + "PRIVATE KEY-----synthetic-truncated")]
    public void IndependentValidatorRejectsResidualGlobalSecretFamilies(string content)
    {
        Assert.False(new RedactionValidator().Validate("file.txt", content, new RedactionLimits()).IsSafe);
    }

    [Theory]
    [InlineData("__KRONXY_" + "REDACTED_FAKE__")]
    [InlineData("__KRONXY_" + "REDACTED_TOKEN__suffix")]
    [InlineData("prefix__KRONXY_" + "REDACTED_TOKEN__")]
    [InlineData("__KRONXY_" + "REDACTED___TOKEN__")]
    public void ValidatorRejectsEveryNonExactKronxyPlaceholder(string content)
    {
        Assert.False(new RedactionValidator().Validate("file.txt", content, new RedactionLimits()).IsSafe);
    }

    [Fact]
    public void ValidatorFailsClosedForInvalidLimits()
    {
        var invalid = new RedactionLimits { MaxFindingsPerFile = 0 };
        Assert.False(new RedactionValidator().Validate("file.env", "PASS" + "WORD=synthetic", invalid).IsSafe);
    }

    [Fact]
    public void CallerControlledLogicalPathIsNeverExposed()
    {
        const string pathMaterial = "synthetic-path-material";
        var request = StructuredRedactionTests.Request(pathMaterial + ".env", "PASS" + "WORD=synthetic");
        var result = new ContentRedactor().Redact(request);
        var serialized = JsonSerializer.Serialize(result);

        Assert.DoesNotContain(pathMaterial, request.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(pathMaterial, serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsafeValidatorResultMakesRedactorFailClosed()
    {
        var redactor = new ContentRedactor(new EmptyRules(), new AlwaysUnsafeValidator());
        var result = redactor.Redact(StructuredRedactionTests.Request("file.txt", "safe"));
        Assert.Equal(RedactionStatus.SensitiveContentRemaining, result.Status);
        Assert.Null(result.Content);
        Assert.Single(result.Findings);
    }

    [Fact]
    public void OverlappingMatchesUsePriorityAndApplyOnce()
    {
        var redactor = new ContentRedactor(new OverlapRules(), new RedactionValidator());
        var result = redactor.Redact(StructuredRedactionTests.Request("file.txt", "abcdef"));
        Assert.Equal("__KRONXY_REDACTED_PRIVATE_KEY__", result.Content);
        Assert.Single(result.Records);
    }

    [Fact]
    public void RecordsSerializeOnlySafeMetadataInDeterministicOrder()
    {
        var result = new ContentRedactor().Redact(StructuredRedactionTests.Request("file.env", "TOKEN=a\nPWD=b"));
        Assert.Equal([1, 2], result.Records.Select(record => record.LineNumber));
        var json = JsonSerializer.Serialize(result.Records);
        Assert.DoesNotContain("\"value\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("matched", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secretValue", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fingerprint", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecordContractCannotStoreSecretOrDerivedData()
    {
        var names = typeof(RedactionRecord).GetProperties().Select(property => property.Name).ToArray();
        Assert.Equal([nameof(RedactionRecord.RelativePath), nameof(RedactionRecord.RuleId), nameof(RedactionRecord.Category),
            nameof(RedactionRecord.LineNumber), nameof(RedactionRecord.MatchCount)], names);
    }

    private static RedactionLimits Limits(int input = 1_000, int output = 1_000, int redactions = 100) => new()
    { MaxInputBytes = input, MaxOutputBytes = output, MaxRedactionsPerFile = redactions, MaxFindingsPerFile = 100, RegexTimeout = TimeSpan.FromMilliseconds(100) };

    private sealed class TimeoutRules : IRedactionRuleSource
    { public IReadOnlyList<RedactionMatch> Find(string logicalPath, string content, RedactionLimits limits) => throw new RegexMatchTimeoutException(); }
    private sealed class EmptyRules : IRedactionRuleSource
    { public IReadOnlyList<RedactionMatch> Find(string logicalPath, string content, RedactionLimits limits) => []; }
    private sealed class OverlapRules : IRedactionRuleSource
    {
        public IReadOnlyList<RedactionMatch> Find(string logicalPath, string content, RedactionLimits limits) =>
        [new(0, 6, "high", RedactionCategory.PrivateKey, "__KRONXY_REDACTED_PRIVATE_KEY__", 1, 1),
         new(1, 3, "low", RedactionCategory.Token, "__KRONXY_REDACTED_TOKEN__", 2, 1)];
    }
    private sealed class AlwaysUnsafeValidator : IRedactionValidator
    {
        public RedactionValidationResult Validate(string logicalPath, string redactedContent, RedactionLimits limits) => new()
        {
            IsSafe = false,
            Findings = [new RedactionFinding { LogicalPath = logicalPath, RuleId = "synthetic-validator", Category = RedactionCategory.GenericSecret,
                LineNumber = 1, Severity = RedactionSeverity.Error, Description = "Contenido no validado." }]
        };
    }
}

using System.Text.Json;
using System.Text.RegularExpressions;
using Kronxy.Context.Packaging;
using Kronxy.Context.Redaction;
using Xunit;

namespace Kronxy.Context.Tests.Packaging;

public sealed class PackageSecurityAndLimitTests
{
    [Fact]
    public async Task RedactionFailureValidatorFindingAndTimeoutPublishNothing()
    {
        using var folder = new TestFolder();
        var cases = new PackageBuilder[]
        {
            Builder(new FailedRedactor(), new RedactionValidator()),
            Builder(new PassRedactor(), new UnsafeValidator()),
            Builder(new TimeoutRedactor(), new RedactionValidator())
        };
        foreach (var builder in cases.Select((value, index) => (value, index)))
        {
            var destination = folder.PathOf(builder.index + ".zip");
            var result = await builder.value.BuildAsync(PackageBuilderTests.Request(destination, PackageBuilderTests.Entry("a.txt", "synthetic")));
            Assert.False(result.IsSuccess);
            Assert.Null(result.PackageSha256);
            Assert.Null(result.PackageSizeBytes);
            Assert.False(File.Exists(destination));
        }
        Assert.Empty(folder.TemporaryFiles());
    }

    [Fact]
    public async Task RequestsEntriesAndFailuresDoNotSerializeOrPrintContentOrPaths()
    {
        using var folder = new TestFolder();
        var material = string.Concat("synthetic", "-sensitive-material");
        var entry = PackageBuilderTests.Entry("folder/" + material + ".txt", material);
        var request = PackageBuilderTests.Request(folder.PathOf(material + ".zip"), entry);
        var result = await Builder(new FailedRedactor(), new RedactionValidator()).BuildAsync(request);
        foreach (var representation in new[] { entry.ToString(), request.ToString(), result.ToString(), JsonSerializer.Serialize(entry), JsonSerializer.Serialize(request), JsonSerializer.Serialize(result) })
            Assert.DoesNotContain(material, representation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 100, 100, 100, 100, 100)]
    [InlineData(1, 0, 100, 100, 100, 100)]
    [InlineData(1, 100, 0, 100, 100, 100)]
    [InlineData(1, 100, 100, 0, 100, 100)]
    [InlineData(1, 100, 100, 100, 0, 100)]
    [InlineData(1, 100, 100, 100, 100, 0)]
    public async Task InvalidLimitsFailClosed(int entries, int entry, int total, int manifest, int package, int path)
    {
        using var folder = new TestFolder();
        var limits = Limits(entries, entry, total, manifest, package, path);
        var request = PackageBuilderTests.Request(folder.PathOf("package.zip"), PackageBuilderTests.Entry("a.txt", "safe")) with { Limits = limits };
        var result = await new PackageBuilder().BuildAsync(request);
        Assert.Equal(PackageBuildStatus.InvalidRequest, result.Status);
        Assert.Empty(folder.TemporaryFiles());
    }

    [Fact]
    public async Task EntryCountExactPassesAndOneMoreFails()
    {
        using var folder = new TestFolder();
        var limits = Limits(entries: 1);
        var exact = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("one.zip"), PackageBuilderTests.Entry("a.txt", "a")) with { Limits = limits });
        var exceeded = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("two.zip"), PackageBuilderTests.Entry("a.txt", "a"), PackageBuilderTests.Entry("b.txt", "b")) with { Limits = limits });
        Assert.True(exact.IsSuccess);
        Assert.Equal(PackageBuildStatus.LimitExceeded, exceeded.Status);
        Assert.False(File.Exists(folder.PathOf("two.zip")));
    }

    [Fact]
    public async Task Utf8EntryAndTotalLimitsUseBytesAndIncludePlaceholderGrowth()
    {
        using var folder = new TestFolder();
        var exact = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("exact.zip"), PackageBuilderTests.Entry("a.txt", "ñ")) with
        { Limits = Limits(entry: 2, total: 2) });
        var exceeded = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("exceeded.zip"), PackageBuilderTests.Entry("a.txt", "ñ")) with
        { Limits = Limits(entry: 1, total: 10) });
        var growth = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("growth.zip"), PackageBuilderTests.Entry("a.env", "PWD=x")) with
        { Limits = Limits(entry: 20, total: 20) });
        Assert.True(exact.IsSuccess);
        Assert.Equal(PackageBuildStatus.LimitExceeded, exceeded.Status);
        Assert.Equal(PackageBuildStatus.LimitExceeded, growth.Status);
    }

    [Fact]
    public async Task PathUtf8LimitIsExact()
    {
        using var folder = new TestFolder();
        var exact = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("one.zip"), PackageBuilderTests.Entry("ñ", "")) with
        { Limits = Limits(path: 2) });
        var exceeded = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("two.zip"), PackageBuilderTests.Entry("ñ", "")) with
        { Limits = Limits(path: 1) });
        Assert.True(exact.IsSuccess);
        Assert.Equal(PackageBuildStatus.InvalidPath, exceeded.Status);
    }

    [Fact]
    public async Task ManifestAndFinalPackageLimitsFailWithoutResidue()
    {
        using var folder = new TestFolder();
        var manifest = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("manifest.zip"), PackageBuilderTests.Entry("a.txt", "a")) with
        { Limits = Limits(manifest: 1) });
        var package = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("package.zip"), PackageBuilderTests.Entry("a.txt", "a")) with
        { Limits = Limits(manifest: 100, package: 100) });
        Assert.Equal(PackageBuildStatus.LimitExceeded, manifest.Status);
        Assert.Equal(PackageBuildStatus.LimitExceeded, package.Status);
        Assert.Empty(folder.TemporaryFiles());
    }

    [Fact]
    public async Task IntegrityFailureDeletesTemporaryAndPublishesNothing()
    {
        using var folder = new TestFolder();
        var builder = new PackageBuilder(new ContentRedactor(), new RedactionValidator(), new AlwaysInvalidVerifier(), new AtomicPackageFileSystem());
        var result = await builder.BuildAsync(PackageBuilderTests.Request(folder.PathOf("package.zip"), PackageBuilderTests.Entry("a.txt", "safe")));
        Assert.Equal(PackageBuildStatus.IntegrityFailure, result.Status);
        Assert.False(File.Exists(folder.PathOf("package.zip")));
        Assert.Empty(folder.TemporaryFiles());
    }

    [Fact]
    public async Task NullOrContradictoryDependencyOutputsFailClosed()
    {
        using var folder = new TestFolder();
        var builders = new[]
        {
            Builder(new NullContentRedactor(), new RedactionValidator()),
            Builder(new FailureWithContentRedactor(), new RedactionValidator()),
            Builder(new NullRecordsRedactor(), new RedactionValidator()),
            Builder(new PassRedactor(), new NullFindingsValidator())
        };
        for (var index = 0; index < builders.Length; index++)
        {
            var destination = folder.PathOf($"invalid-{index}.zip");
            var result = await builders[index].BuildAsync(PackageBuilderTests.Request(destination,
                PackageBuilderTests.Entry("a.txt", "safe")));
            Assert.False(result.IsSuccess);
            Assert.Null(result.PackageSha256);
            Assert.False(File.Exists(destination));
        }
        Assert.Empty(folder.TemporaryFiles());
    }

    [Fact]
    public async Task DependencyExceptionsReturnGenericFailureWithoutPublication()
    {
        using var folder = new TestFolder();
        var redactorFailure = await Builder(new ThrowingRedactor(), new RedactionValidator()).BuildAsync(
            PackageBuilderTests.Request(folder.PathOf("redactor.zip"), PackageBuilderTests.Entry("a.txt", "safe")));
        var validatorFailure = await Builder(new PassRedactor(), new ThrowingValidator()).BuildAsync(
            PackageBuilderTests.Request(folder.PathOf("validator.zip"), PackageBuilderTests.Entry("a.txt", "safe")));
        Assert.Equal(PackageBuildStatus.InternalFailure, redactorFailure.Status);
        Assert.Equal(PackageBuildStatus.InternalFailure, validatorFailure.Status);
        Assert.Empty(Directory.GetFiles(folder.Root, "*.zip"));
        Assert.Empty(folder.TemporaryFiles());
    }

    [Theory]
    [InlineData("remaining")]
    [InlineData("placeholder")]
    public async Task IndependentValidatorRejectsUnsafeRedactorOutput(string scenario)
    {
        using var folder = new TestFolder();
        var content = scenario == "remaining"
            ? "PASSWORD=" + string.Concat("synthetic", "-remaining-material")
            : "value=__KRONXY_REDACTED_UNKNOWN__";
        var result = await Builder(new PassRedactor(), new RedactionValidator()).BuildAsync(
            PackageBuilderTests.Request(folder.PathOf("package.zip"), PackageBuilderTests.Entry("a.env", content)));
        Assert.Equal(PackageBuildStatus.SensitiveContentRemaining, result.Status);
        Assert.False(File.Exists(folder.PathOf("package.zip")));
        Assert.Empty(folder.TemporaryFiles());
    }

    [Fact]
    public void InternalPreparedEntryDefaultSerializationDoesNotExposePathOrContent()
    {
        var marker = string.Concat("synthetic", "-prepared-marker");
        var bytes = System.Text.Encoding.UTF8.GetBytes(marker);
        var entry = new PreparedPackageEntry("folder/" + marker + ".txt", PackageEntryKind.Source,
            bytes, PackageBuilder.Hash(bytes), []);
        foreach (var representation in new[] { JsonSerializer.Serialize(entry), entry.ToString() })
        {
            Assert.DoesNotContain(marker, representation, StringComparison.Ordinal);
            Assert.DoesNotContain(Convert.ToBase64String(bytes), representation, StringComparison.Ordinal);
        }
    }

    private static PackageBuildLimits Limits(int entries = 10, int entry = 100, int total = 1000, int manifest = 1000, int package = 4000, int path = 100) => new()
    {
        MaxEntries = entries, MaxEntryBytes = entry, MaxTotalContentBytes = total, MaxManifestBytes = manifest,
        MaxPackageBytes = package, MaxPathBytes = path,
        Redaction = new RedactionLimits { MaxInputBytes = 1000, MaxOutputBytes = 1000, MaxRedactionsPerFile = 100, MaxFindingsPerFile = 100, RegexTimeout = TimeSpan.FromMilliseconds(100) }
    };

    private static PackageBuilder Builder(IContentRedactor redactor, IRedactionValidator validator) =>
        new(redactor, validator, new PackageIntegrityVerifier(), new AtomicPackageFileSystem());

    private sealed class FailedRedactor : IContentRedactor
    { public RedactionResult Redact(RedactionRequest request) => new() { Status = RedactionStatus.InternalError }; }
    private sealed class TimeoutRedactor : IContentRedactor
    { public RedactionResult Redact(RedactionRequest request) => new() { Status = RedactionStatus.RuleTimeout }; }
    private sealed class PassRedactor : IContentRedactor
    { public RedactionResult Redact(RedactionRequest request) => new() { Status = RedactionStatus.SuccessUnchanged, Content = request.Content }; }
    private sealed class UnsafeValidator : IRedactionValidator
    { public RedactionValidationResult Validate(string logicalPath, string redactedContent, RedactionLimits limits) => new() { IsSafe = false }; }
    private sealed class AlwaysInvalidVerifier : IPackageIntegrityVerifier
    { public Task<bool> VerifyAsync(Stream package, IReadOnlyList<PreparedPackageEntry> expectedEntries, byte[] expectedManifest, PackageBuildLimits limits, CancellationToken cancellationToken) => Task.FromResult(false); }

    private sealed class NullContentRedactor : IContentRedactor
    { public RedactionResult Redact(RedactionRequest request) => new() { Status = RedactionStatus.SuccessRedacted, Content = null }; }
    private sealed class FailureWithContentRedactor : IContentRedactor
    { public RedactionResult Redact(RedactionRequest request) => new() { Status = RedactionStatus.InternalError, Content = "safe" }; }
    private sealed class NullRecordsRedactor : IContentRedactor
    { public RedactionResult Redact(RedactionRequest request) => new() { Status = RedactionStatus.SuccessUnchanged, Content = request.Content, Records = null! }; }
    private sealed class NullFindingsValidator : IRedactionValidator
    { public RedactionValidationResult Validate(string logicalPath, string redactedContent, RedactionLimits limits) => new() { IsSafe = true, Findings = null! }; }
    private sealed class ThrowingRedactor : IContentRedactor
    { public RedactionResult Redact(RedactionRequest request) => throw new InvalidOperationException("synthetic redactor failure"); }
    private sealed class ThrowingValidator : IRedactionValidator
    { public RedactionValidationResult Validate(string logicalPath, string redactedContent, RedactionLimits limits) => throw new InvalidOperationException("synthetic validator failure"); }
}

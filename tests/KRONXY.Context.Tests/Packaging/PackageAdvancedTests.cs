using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kronxy.Context.Models;
using Kronxy.Context.Packaging;
using Kronxy.Context.Redaction;
using Xunit;

namespace Kronxy.Context.Tests.Packaging;

public sealed class PackageAdvancedTests
{
    [Fact]
    public async Task TenBuildsFromEquivalentRequestsProduceIdenticalBytes()
    {
        using var folder = new TestFolder();
        byte[]? baseline = null;
        string? baselineHash = null;
        for (var index = 0; index < 10; index++)
        {
            var entries = index % 2 == 0
                ? new[] { PackageBuilderTests.Entry("z.txt", "z\n"), PackageBuilderTests.Entry("a.txt", "a\r\n") }
                : new[] { PackageBuilderTests.Entry("a.txt", "a\r\n"), PackageBuilderTests.Entry("z.txt", "z\n") };
            var result = await new PackageBuilder().BuildAsync(
                PackageBuilderTests.Request(folder.PathOf($"package-{index}.zip"), entries));
            Assert.True(result.IsSuccess);
            var bytes = await File.ReadAllBytesAsync(folder.PathOf($"package-{index}.zip"));
            baseline ??= bytes;
            baselineHash ??= result.PackageSha256;
            Assert.Equal(baseline, bytes);
            Assert.Equal(baselineHash, result.PackageSha256);
        }
    }

    [Fact]
    public async Task ManifestIsCanonicalUtf8WithoutBomOrTrailingNewline()
    {
        using var folder = new TestFolder();
        var destination = folder.PathOf("package.zip");
        var result = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(destination,
            new PackageEntryRequest { LogicalPath = "unicode/\u03b1.txt", Kind = PackageEntryKind.Metadata, Content = "\u00f1\n" }));
        Assert.True(result.IsSuccess);

        using var archive = ZipFile.OpenRead(destination);
        var entry = archive.GetEntry(PackagePath.ManifestName)!;
        await using var source = entry.Open();
        using var memory = new MemoryStream();
        await source.CopyToAsync(memory);
        var bytes = memory.ToArray();
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.NotEqual((byte)'\n', bytes[^1]);
        var json = Encoding.UTF8.GetString(bytes);
        Assert.True(json.IndexOf("\"schemaVersion\"", StringComparison.Ordinal) < json.IndexOf("\"generatorVersion\"", StringComparison.Ordinal));
        Assert.True(json.IndexOf("\"generatorVersion\"", StringComparison.Ordinal) < json.IndexOf("\"packageId\"", StringComparison.Ordinal));
        using var document = JsonDocument.Parse(bytes);
        Assert.Equal(64, document.RootElement.GetProperty("packageId").GetString()!.Length);
        Assert.Matches("^[0-9a-f]{64}$", result.PackageSha256!);
    }

    [Theory]
    [InlineData(PackageEntryKind.Source)]
    [InlineData(PackageEntryKind.Patch)]
    [InlineData(PackageEntryKind.Diagnostic)]
    [InlineData(PackageEntryKind.Metadata)]
    public async Task EveryDeclaredEntryKindIsRecorded(PackageEntryKind kind)
    {
        using var folder = new TestFolder();
        var destination = folder.PathOf("package.zip");
        var request = PackageBuilderTests.Request(destination,
            new PackageEntryRequest { LogicalPath = "entry.txt", Kind = kind, Content = string.Empty });
        Assert.True((await new PackageBuilder().BuildAsync(request)).IsSuccess);
        using var archive = ZipFile.OpenRead(destination);
        using var manifest = JsonDocument.Parse(await PackageBuilderTests.ReadAsync(archive.GetEntry(PackagePath.ManifestName)!));
        Assert.Equal(kind.ToString(), manifest.RootElement.GetProperty("entries")[0].GetProperty("kind").GetString());
    }

    [Fact]
    public async Task InvalidEntryAndRequestMetadataFailBeforeCreatingTemporaryFiles()
    {
        using var folder = new TestFolder();
        var invalidKind = PackageBuilderTests.Entry("a.txt", "safe") with { Kind = (PackageEntryKind)999 };
        var invalidKindResult = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("kind.zip"), invalidKind));
        var nullEntryResult = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("null.zip"), [null!]));
        var localTimeResult = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("time.zip")) with
        { GeneratedAtUtc = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.FromHours(1)) });

        Assert.Equal(PackageBuildStatus.InvalidRequest, invalidKindResult.Status);
        Assert.Equal(PackageBuildStatus.InvalidRequest, nullEntryResult.Status);
        Assert.Equal(PackageBuildStatus.InvalidRequest, localTimeResult.Status);
        Assert.Empty(folder.TemporaryFiles());
        Assert.Empty(Directory.GetFiles(folder.Root, "*.zip"));
    }

    [Theory]
    [InlineData(0, 1, 1, 1, 1, 1)]
    [InlineData(-1, 1, 1, 1, 1, 1)]
    [InlineData(1, 2, 1, 1, 10, 1)]
    [InlineData(1, 1, 1, 11, 10, 1)]
    public async Task InvalidAndIncoherentLimitConfigurationsFailClosed(
        int entries, int entryBytes, int totalBytes, int manifestBytes, int packageBytes, int pathBytes)
    {
        using var folder = new TestFolder();
        var request = PackageBuilderTests.Request(folder.PathOf("package.zip")) with
        {
            Limits = Limits(entries, entryBytes, totalBytes, manifestBytes, packageBytes, pathBytes)
        };
        var result = await new PackageBuilder().BuildAsync(request);
        Assert.Equal(PackageBuildStatus.InvalidRequest, result.Status);
        Assert.Empty(folder.TemporaryFiles());
    }

    [Fact]
    public async Task ManifestAndPackageExactByteLimitsPassAndOneLessFails()
    {
        using var folder = new TestFolder();
        var firstDestination = folder.PathOf("measure.zip");
        Assert.True((await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(firstDestination,
            PackageBuilderTests.Entry("a.txt", "safe")))).IsSuccess);
        using var archive = ZipFile.OpenRead(firstDestination);
        var manifestLength = checked((int)archive.GetEntry(PackagePath.ManifestName)!.Length);
        var packageLength = new FileInfo(firstDestination).Length;

        var exactLimits = Limits(10, 100, 100, manifestLength, packageLength, 100);
        var exact = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("exact.zip"),
            PackageBuilderTests.Entry("a.txt", "safe")) with { Limits = exactLimits });
        var manifestExceeded = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("manifest-fail.zip"),
            PackageBuilderTests.Entry("a.txt", "safe")) with { Limits = exactLimits with { MaxManifestBytes = manifestLength - 1 } });
        var packageExceeded = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("package-fail.zip"),
            PackageBuilderTests.Entry("a.txt", "safe")) with { Limits = exactLimits with { MaxPackageBytes = packageLength - 1 } });

        Assert.True(exact.IsSuccess);
        Assert.Equal(PackageBuildStatus.LimitExceeded, manifestExceeded.Status);
        Assert.Equal(PackageBuildStatus.LimitExceeded, packageExceeded.Status);
        Assert.False(File.Exists(folder.PathOf("manifest-fail.zip")));
        Assert.False(File.Exists(folder.PathOf("package-fail.zip")));
        Assert.Empty(folder.TemporaryFiles());
    }

    [Fact]
    public async Task TotalUtf8LimitIsCheckedAtExactBoundaryAcrossEntries()
    {
        using var folder = new TestFolder();
        var entries = new[] { PackageBuilderTests.Entry("a.txt", "\u00f1"), PackageBuilderTests.Entry("b.txt", "x") };
        var exactLimits = Limits(2, 2, 3, 2_000, 4_000, 100);
        var exact = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("exact.zip"), entries) with { Limits = exactLimits });
        var exceeded = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("fail.zip"), entries) with
        { Limits = exactLimits with { MaxTotalContentBytes = 2 } });
        Assert.True(exact.IsSuccess);
        Assert.Equal(PackageBuildStatus.LimitExceeded, exceeded.Status);
        Assert.Empty(folder.TemporaryFiles());
    }

    [Fact]
    public async Task UntrustedRedactionCategoryCannotReachManifestOrResult()
    {
        using var folder = new TestFolder();
        var marker = string.Concat("synthetic", "-metadata-marker");
        var redactor = new InvalidMetadataRedactor(marker);
        var builder = new PackageBuilder(redactor, new AlwaysSafeValidator(), new PackageIntegrityVerifier(), new AtomicPackageFileSystem());
        var result = await builder.BuildAsync(PackageBuilderTests.Request(folder.PathOf("package.zip"),
            PackageBuilderTests.Entry("a.txt", "safe")));
        Assert.Equal(PackageBuildStatus.RedactionFailed, result.Status);
        Assert.DoesNotContain(marker, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(marker, JsonSerializer.Serialize(result), StringComparison.Ordinal);
        Assert.False(File.Exists(folder.PathOf("package.zip")));
        Assert.Empty(folder.TemporaryFiles());
    }

    [Theory]
    [InlineData(RedactionStatus.InputLimitExceeded, PackageBuildStatus.LimitExceeded)]
    [InlineData(RedactionStatus.OutputLimitExceeded, PackageBuildStatus.LimitExceeded)]
    [InlineData(RedactionStatus.TooManyRedactions, PackageBuildStatus.LimitExceeded)]
    [InlineData(RedactionStatus.RuleTimeout, PackageBuildStatus.RedactionFailed)]
    [InlineData(RedactionStatus.UnprocessableFormat, PackageBuildStatus.RedactionFailed)]
    public async Task RedactionFailuresMapDeterministicallyAndPublishNothing(
        RedactionStatus redactionStatus, PackageBuildStatus packageStatus)
    {
        using var folder = new TestFolder();
        var builder = new PackageBuilder(new StatusRedactor(redactionStatus), new AlwaysSafeValidator(),
            new PackageIntegrityVerifier(), new AtomicPackageFileSystem());
        var result = await builder.BuildAsync(PackageBuilderTests.Request(folder.PathOf("package.zip"),
            PackageBuilderTests.Entry("a.txt", "safe")));
        Assert.Equal(packageStatus, result.Status);
        Assert.Null(result.PackageSha256);
        Assert.Null(result.PackageSizeBytes);
        Assert.Empty(folder.TemporaryFiles());
    }

    [Fact]
    public async Task HashUsesOnlyFinalRedactedUtf8Bytes()
    {
        using var folder = new TestFolder();
        var original = string.Concat("synthetic", "-original-material");
        const string replacement = "synthetic-redacted-output";
        var builder = new PackageBuilder(new ReplacementRedactor(replacement), new AlwaysSafeValidator(),
            new PackageIntegrityVerifier(), new AtomicPackageFileSystem());
        var destination = folder.PathOf("package.zip");
        var result = await builder.BuildAsync(PackageBuilderTests.Request(destination,
            PackageBuilderTests.Entry("a.txt", original)));
        Assert.True(result.IsSuccess);
        using var archive = ZipFile.OpenRead(destination);
        using var manifest = JsonDocument.Parse(await PackageBuilderTests.ReadAsync(archive.GetEntry(PackagePath.ManifestName)!));
        var hash = manifest.RootElement.GetProperty("entries")[0].GetProperty("sha256").GetString();
        Assert.Equal(PackageBuilder.Hash(Encoding.UTF8.GetBytes(replacement)), hash);
        Assert.NotEqual(PackageBuilder.Hash(Encoding.UTF8.GetBytes(original)), hash);
        Assert.True((await File.ReadAllBytesAsync(destination)).AsSpan().IndexOf(Encoding.UTF8.GetBytes(original)) < 0);
    }

    [Fact]
    public async Task DestinationAndPersonalPathMarkersNeverEnterPackageBytes()
    {
        using var folder = new TestFolder();
        var marker = string.Concat("synthetic", "-personal-path-marker");
        var destination = folder.PathOf(marker + ".zip");
        var result = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(destination,
            PackageBuilderTests.Entry("safe.txt", "safe")));
        Assert.True(result.IsSuccess);
        Assert.True((await File.ReadAllBytesAsync(destination)).AsSpan().IndexOf(Encoding.UTF8.GetBytes(marker)) < 0);
    }

    [Theory]
    [InlineData("relative.zip")]
    [InlineData("")]
    [InlineData(" ")]
    public async Task InvalidDestinationFailsBeforeTemporaryCreation(string destination)
    {
        using var folder = new TestFolder();
        var result = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(destination,
            PackageBuilderTests.Entry("a.txt", "safe")));
        Assert.Equal(PackageBuildStatus.InvalidPath, result.Status);
        Assert.Empty(folder.TemporaryFiles());
    }

    [Fact]
    public async Task ContradictoryValidatorResultFailsClosedBeforeHashingOrPublication()
    {
        using var folder = new TestFolder();
        var material = string.Concat("synthetic", "-validator-material");
        var builder = new PackageBuilder(new PassThroughRedactor(), new ContradictoryValidator(),
            new PackageIntegrityVerifier(), new AtomicPackageFileSystem());
        var destination = folder.PathOf("package.zip");
        var result = await builder.BuildAsync(PackageBuilderTests.Request(destination,
            PackageBuilderTests.Entry("a.txt", material)));
        Assert.Equal(PackageBuildStatus.SensitiveContentRemaining, result.Status);
        Assert.Null(result.PackageSha256);
        Assert.False(File.Exists(destination));
        Assert.Empty(folder.TemporaryFiles());
    }

    [Theory]
    [InlineData("redactor-finding")]
    [InlineData("unchanged-with-changed-content")]
    [InlineData("redacted-without-records")]
    public async Task ContradictoryRedactorResultFailsClosed(string contradiction)
    {
        using var folder = new TestFolder();
        var builder = new PackageBuilder(new ContradictoryRedactor(contradiction), new AlwaysSafeValidator(),
            new PackageIntegrityVerifier(), new AtomicPackageFileSystem());
        var result = await builder.BuildAsync(PackageBuilderTests.Request(folder.PathOf("package.zip"),
            PackageBuilderTests.Entry("a.txt", "safe-input")));
        Assert.Equal(PackageBuildStatus.RedactionFailed, result.Status);
        Assert.False(File.Exists(folder.PathOf("package.zip")));
        Assert.Empty(folder.TemporaryFiles());
    }

    [Fact]
    public async Task CancellationRaisedByRedactorStopsBeforeValidator()
    {
        using var folder = new TestFolder();
        using var cancellation = new CancellationTokenSource();
        var validator = new TrackingValidator();
        var builder = new PackageBuilder(new CancellingRedactor(cancellation), validator,
            new PackageIntegrityVerifier(), new AtomicPackageFileSystem());
        var result = await builder.BuildAsync(PackageBuilderTests.Request(folder.PathOf("package.zip"),
            PackageBuilderTests.Entry("a.txt", "safe")), cancellation.Token);
        Assert.Equal(PackageBuildStatus.Cancelled, result.Status);
        Assert.False(validator.WasCalled);
        Assert.False(File.Exists(folder.PathOf("package.zip")));
    }

    [Fact]
    public void PackageIdBindsOrderedRedactionSummaries()
    {
        var content = Encoding.UTF8.GetBytes("safe");
        var first = new PreparedPackageEntry("a.txt", PackageEntryKind.Source, content,
            PackageBuilder.Hash(content), [new PreparedRedactionSummary("Password", 1)]);
        var second = first with { Redactions = [new PreparedRedactionSummary("Token", 1)] };
        var request = PackageBuilderTests.Request(Path.Combine(Path.GetTempPath(), "unused.zip"));
        var firstManifest = PackageBuilder.CreateManifest(request, [first], content.Length);
        var secondManifest = PackageBuilder.CreateManifest(request, [second], content.Length);
        Assert.NotEqual(firstManifest.PackageId, secondManifest.PackageId);
    }

    [Fact]
    public void PackageIdUsesUnambiguousLengthPrefixedFields()
    {
        var first = PackageBuilder.ComputePackageId("a", "b\nc", "type", "stability", "time", []);
        var second = PackageBuilder.ComputePackageId("a\nb", "c", "type", "stability", "time", []);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task MaximumSizedLimitConfigurationRemainsValidForSmallInput()
    {
        using var folder = new TestFolder();
        var limits = new PackageBuildLimits
        {
            MaxEntries = int.MaxValue,
            MaxEntryBytes = int.MaxValue,
            MaxTotalContentBytes = long.MaxValue,
            MaxManifestBytes = int.MaxValue,
            MaxPackageBytes = long.MaxValue,
            MaxPathBytes = int.MaxValue,
            Redaction = new RedactionLimits
            {
                MaxInputBytes = int.MaxValue,
                MaxOutputBytes = int.MaxValue,
                MaxRedactionsPerFile = int.MaxValue,
                MaxFindingsPerFile = int.MaxValue,
                RegexTimeout = TimeSpan.FromMilliseconds(250)
            }
        };
        var result = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("package.zip"),
            PackageBuilderTests.Entry("a.txt", "safe")) with { Limits = limits });
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("output")]
    [InlineData("records")]
    public async Task DefectiveRedactorCannotBypassConfiguredRedactionLimits(string scenario)
    {
        using var folder = new TestFolder();
        var redactionLimits = new RedactionLimits
        {
            MaxInputBytes = 100,
            MaxOutputBytes = scenario == "output" ? 4 : 100,
            MaxRedactionsPerFile = scenario == "records" ? 1 : 100,
            MaxFindingsPerFile = 100,
            RegexTimeout = TimeSpan.FromMilliseconds(100)
        };
        var limits = new PackageBuildLimits { MaxEntryBytes = 100, Redaction = redactionLimits };
        IContentRedactor redactor = scenario == "output"
            ? new ExcessiveOutputRedactor()
            : new ExcessiveRecordsRedactor();
        var builder = new PackageBuilder(redactor, new AlwaysSafeValidator(),
            new PackageIntegrityVerifier(), new AtomicPackageFileSystem());
        var result = await builder.BuildAsync(PackageBuilderTests.Request(folder.PathOf("package.zip"),
            PackageBuilderTests.Entry("a.txt", "safe")) with { Limits = limits });
        Assert.Equal(PackageBuildStatus.LimitExceeded, result.Status);
        Assert.False(File.Exists(folder.PathOf("package.zip")));
        Assert.Empty(folder.TemporaryFiles());
    }

    [Fact]
    public async Task MalformedUtf16PathIsRejectedAsInvalidPath()
    {
        using var folder = new TestFolder();
        var malformed = "folder/" + '\ud800' + ".txt";
        var result = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("package.zip"),
            PackageBuilderTests.Entry(malformed, "safe")));
        Assert.Equal(PackageBuildStatus.InvalidPath, result.Status);
        Assert.False(File.Exists(folder.PathOf("package.zip")));
    }

    [Fact]
    public async Task PortableSegmentLimitAccepts255Utf8BytesAndRejects256()
    {
        using var folder = new TestFolder();
        var accepted = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("accepted.zip"),
            PackageBuilderTests.Entry(new string('a', 255), string.Empty)));
        var rejected = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("rejected.zip"),
            PackageBuilderTests.Entry(new string('a', 256), string.Empty)));
        Assert.True(accepted.IsSuccess);
        Assert.Equal(PackageBuildStatus.InvalidPath, rejected.Status);
        Assert.False(File.Exists(folder.PathOf("rejected.zip")));
    }

    [Fact]
    public async Task ValidEmojiIsAcceptedButUnicodeFormatControlIsRejected()
    {
        using var folder = new TestFolder();
        var emoji = char.ConvertFromUtf32(0x1F680) + ".txt";
        var accepted = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("emoji.zip"),
            PackageBuilderTests.Entry(emoji, "safe")));
        var rejected = await new PackageBuilder().BuildAsync(PackageBuilderTests.Request(folder.PathOf("control.zip"),
            PackageBuilderTests.Entry("ab\u202Ecd.txt", "safe")));
        Assert.True(accepted.IsSuccess);
        Assert.Equal(PackageBuildStatus.InvalidPath, rejected.Status);
        Assert.False(File.Exists(folder.PathOf("control.zip")));
    }

    private static PackageBuildLimits Limits(int entries, int entryBytes, long totalBytes,
        int manifestBytes, long packageBytes, int pathBytes) => new()
    {
        MaxEntries = entries,
        MaxEntryBytes = entryBytes,
        MaxTotalContentBytes = totalBytes,
        MaxManifestBytes = manifestBytes,
        MaxPackageBytes = packageBytes,
        MaxPathBytes = pathBytes,
        Redaction = new RedactionLimits
        {
            MaxInputBytes = Math.Max(entryBytes, 1),
            MaxOutputBytes = Math.Max(entryBytes, 1),
            MaxRedactionsPerFile = 100,
            MaxFindingsPerFile = 100,
            RegexTimeout = TimeSpan.FromMilliseconds(100)
        }
    };

    private sealed class InvalidMetadataRedactor(string category) : IContentRedactor
    {
        public RedactionResult Redact(RedactionRequest request) => new()
        {
            Status = RedactionStatus.SuccessRedacted,
            Content = "changed-safe",
            Records = [new RedactionRecord { Category = category, MatchCount = 1 }]
        };
    }

    private sealed class StatusRedactor(RedactionStatus status) : IContentRedactor
    {
        public RedactionResult Redact(RedactionRequest request) => new() { Status = status };
    }

    private sealed class ReplacementRedactor(string replacement) : IContentRedactor
    {
        public RedactionResult Redact(RedactionRequest request) => new()
        {
            Status = RedactionStatus.SuccessRedacted,
            Content = replacement,
            Records = [new RedactionRecord { Category = nameof(RedactionCategory.GenericSecret), MatchCount = 1 }]
        };
    }

    private sealed class AlwaysSafeValidator : IRedactionValidator
    {
        public RedactionValidationResult Validate(string logicalPath, string redactedContent, RedactionLimits limits) =>
            new() { IsSafe = true };
    }

    private sealed class PassThroughRedactor : IContentRedactor
    {
        public RedactionResult Redact(RedactionRequest request) => new()
        {
            Status = RedactionStatus.SuccessUnchanged,
            Content = request.Content
        };
    }

    private sealed class ContradictoryValidator : IRedactionValidator
    {
        public RedactionValidationResult Validate(string logicalPath, string redactedContent, RedactionLimits limits) => new()
        {
            IsSafe = true,
            Findings = [Finding()]
        };
    }

    private sealed class ContradictoryRedactor(string contradiction) : IContentRedactor
    {
        public RedactionResult Redact(RedactionRequest request) => contradiction switch
        {
            "redactor-finding" => new RedactionResult
            {
                Status = RedactionStatus.SuccessUnchanged,
                Content = request.Content,
                Findings = [Finding()]
            },
            "unchanged-with-changed-content" => new RedactionResult
            {
                Status = RedactionStatus.SuccessUnchanged,
                Content = "changed"
            },
            _ => new RedactionResult
            {
                Status = RedactionStatus.SuccessRedacted,
                Content = "changed"
            }
        };
    }

    private sealed class CancellingRedactor(CancellationTokenSource cancellation) : IContentRedactor
    {
        public RedactionResult Redact(RedactionRequest request)
        {
            cancellation.Cancel();
            return new RedactionResult { Status = RedactionStatus.SuccessUnchanged, Content = request.Content };
        }
    }

    private sealed class TrackingValidator : IRedactionValidator
    {
        public bool WasCalled { get; private set; }
        public RedactionValidationResult Validate(string logicalPath, string redactedContent, RedactionLimits limits)
        {
            WasCalled = true;
            return new RedactionValidationResult { IsSafe = true };
        }
    }

    private sealed class ExcessiveOutputRedactor : IContentRedactor
    {
        public RedactionResult Redact(RedactionRequest request) => new()
        {
            Status = RedactionStatus.SuccessRedacted,
            Content = "output-too-large",
            Records = [new RedactionRecord { Category = nameof(RedactionCategory.GenericSecret), MatchCount = 1 }]
        };
    }

    private sealed class ExcessiveRecordsRedactor : IContentRedactor
    {
        public RedactionResult Redact(RedactionRequest request) => new()
        {
            Status = RedactionStatus.SuccessRedacted,
            Content = "changed",
            Records = [new RedactionRecord { Category = nameof(RedactionCategory.GenericSecret), MatchCount = 2 }]
        };
    }

    private static RedactionFinding Finding() => new()
    {
        RuleId = "synthetic-finding",
        Category = RedactionCategory.GenericSecret,
        LineNumber = 1,
        Severity = RedactionSeverity.Error,
        Description = "Synthetic finding."
    };
}

using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kronxy.Context.Redaction;

namespace Kronxy.Context.Packaging;

public sealed class PackageBuilder : IPackageBuilder
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions ManifestJson = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private readonly IContentRedactor redactor;
    private readonly IRedactionValidator validator;
    private readonly IPackageIntegrityVerifier verifier;
    private readonly IAtomicPackageFileSystem fileSystem;

    public PackageBuilder() : this(new ContentRedactor(), new RedactionValidator(),
        new PackageIntegrityVerifier(), new AtomicPackageFileSystem()) { }

    internal PackageBuilder(IContentRedactor redactor, IRedactionValidator validator,
        IPackageIntegrityVerifier verifier, IAtomicPackageFileSystem fileSystem)
    {
        this.redactor = redactor;
        this.validator = validator;
        this.verifier = verifier;
        this.fileSystem = fileSystem;
    }

    public async Task<PackageBuildResult> BuildAsync(PackageBuildRequest request, CancellationToken cancellationToken = default)
    {
        string? temporaryPath = null;
        var ownsTemporary = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsValidRequest(request)) return Failure(PackageBuildStatus.InvalidRequest);
            if (!TryValidateDestination(request.DestinationPath, out var destination)) return Failure(PackageBuildStatus.InvalidPath);
            if (fileSystem.Exists(destination)) return Failure(PackageBuildStatus.DestinationExists);
            if (request.Entries.Count > request.Limits.MaxEntries) return Failure(PackageBuildStatus.LimitExceeded);

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var canonicalEntries = new List<(PackageEntryRequest Entry, string Path)>();
            foreach (var entry in request.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry is null || entry.Content is null || !Enum.IsDefined(entry.Kind))
                    return Failure(PackageBuildStatus.InvalidRequest);
                if (!PackagePath.TryCanonicalize(entry.LogicalPath, request.Limits.MaxPathBytes, out var path))
                    return Failure(PackageBuildStatus.InvalidPath);
                if (!paths.Add(path)) return Failure(PackageBuildStatus.DuplicateEntry);
                canonicalEntries.Add((entry, path));
            }

            var prepared = new List<PreparedPackageEntry>(canonicalEntries.Count);
            long totalBytes = 0;
            foreach (var item in canonicalEntries.OrderBy(item => item.Path, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                int inputBytes;
                try { inputBytes = StrictUtf8.GetByteCount(item.Entry.Content); }
                catch (EncoderFallbackException) { return Failure(PackageBuildStatus.RedactionFailed); }
                if (inputBytes > request.Limits.MaxEntryBytes || inputBytes > request.Limits.Redaction.MaxInputBytes)
                    return Failure(PackageBuildStatus.LimitExceeded);

                var redaction = redactor.Redact(new RedactionRequest
                {
                    LogicalPath = item.Path,
                    Content = item.Entry.Content,
                    Limits = request.Limits.Redaction
                });
                if (!redaction.IsSuccess || redaction.Content is null)
                    return Failure(MapRedactionFailure(redaction.Status));
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsConsistentRedactionResult(item.Entry.Content, redaction))
                    return Failure(PackageBuildStatus.RedactionFailed);
                if (ExceedsRedactionRecordLimit(redaction.Records, request.Limits.Redaction.MaxRedactionsPerFile))
                    return Failure(PackageBuildStatus.LimitExceeded);
                if (!TryCreateRedactionSummaries(redaction.Records, out var summaries))
                    return Failure(PackageBuildStatus.RedactionFailed);

                var validation = validator.Validate(item.Path, redaction.Content, request.Limits.Redaction);
                cancellationToken.ThrowIfCancellationRequested();
                if (validation is null || !validation.IsSafe || validation.Findings is null || validation.Findings.Count != 0)
                    return Failure(PackageBuildStatus.SensitiveContentRemaining);

                var bytes = StrictUtf8.GetBytes(redaction.Content);
                if (bytes.Length > request.Limits.MaxEntryBytes ||
                    bytes.Length > request.Limits.Redaction.MaxOutputBytes)
                    return Failure(PackageBuildStatus.LimitExceeded);
                totalBytes = checked(totalBytes + bytes.LongLength);
                if (totalBytes > request.Limits.MaxTotalContentBytes) return Failure(PackageBuildStatus.LimitExceeded);
                prepared.Add(new PreparedPackageEntry(item.Path, item.Entry.Kind, bytes, Hash(bytes), summaries));
            }

            var manifest = CreateManifest(request, prepared, totalBytes);
            var manifestBytes = SerializeManifest(manifest);
            if (manifestBytes.Length > request.Limits.MaxManifestBytes) return Failure(PackageBuildStatus.LimitExceeded);

            cancellationToken.ThrowIfCancellationRequested();
            temporaryPath = fileSystem.CreateTemporaryPath(destination);
            if (!IsSafeTemporaryPath(destination, temporaryPath))
                return Failure(PackageBuildStatus.IoFailure);
            long packageLength;
            string temporaryHash;
            await using (var stream = fileSystem.CreateNew(temporaryPath))
            {
                ownsTemporary = true;
                WriteZip(stream, prepared, manifestBytes, request.GeneratedAtUtc, cancellationToken);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                packageLength = stream.Length;
                if (packageLength <= 0) return Failure(PackageBuildStatus.IntegrityFailure);
                if (packageLength > request.Limits.MaxPackageBytes) return Failure(PackageBuildStatus.LimitExceeded);
                var integrity = await verifier.VerifyAsync(stream, prepared, manifestBytes,
                    request.Limits, cancellationToken).ConfigureAwait(false);
                if (!integrity) return Failure(PackageBuildStatus.IntegrityFailure);
                stream.Position = 0;
                temporaryHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)
                    .ConfigureAwait(false)).ToLowerInvariant();
            }

            cancellationToken.ThrowIfCancellationRequested();
            var publishStatus = fileSystem.Publish(temporaryPath, destination);
            if (publishStatus == PackagePublishStatus.DestinationExists)
                return Failure(PackageBuildStatus.DestinationExists);
            if (publishStatus != PackagePublishStatus.Success)
                return Failure(PackageBuildStatus.IoFailure);
            temporaryPath = null;
            ownsTemporary = false;

            var finalValidation = await ValidatePublishedPackageAsync(destination, prepared, manifestBytes,
                request.Limits, temporaryHash, packageLength).ConfigureAwait(false);
            if (finalValidation.Status != PackageBuildStatus.Success)
            {
                TryDeleteWithoutThrowing(destination);
                return Failure(finalValidation.Status);
            }
            return new PackageBuildResult
            {
                Status = PackageBuildStatus.Success,
                PackageSha256 = finalValidation.Hash,
                PackageSizeBytes = finalValidation.Length,
                EntryCount = prepared.Count
            };
        }
        catch (OperationCanceledException) { return Failure(PackageBuildStatus.Cancelled); }
        catch (OverflowException) { return Failure(PackageBuildStatus.LimitExceeded); }
        catch (IOException) { return Failure(PackageBuildStatus.IoFailure); }
        catch (UnauthorizedAccessException) { return Failure(PackageBuildStatus.IoFailure); }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Failure(PackageBuildStatus.InternalFailure);
        }
        finally
        {
            if (ownsTemporary && temporaryPath is not null) TryDeleteWithoutThrowing(temporaryPath);
        }
    }

    private static bool IsValidRequest(PackageBuildRequest? request) => request is not null &&
        request.Entries is not null && request.Limits is not null && request.Limits.IsValid() &&
        request.GeneratedAtUtc.Offset == TimeSpan.Zero && request.GeneratedAtUtc.Year is >= 1980 and <= 2107 &&
        Enum.IsDefined(request.PackageType) && Enum.IsDefined(request.Stability);

    private static bool TryValidateDestination(string? path, out string destination)
    {
        destination = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || path.IndexOfAny(['\0', '\r', '\n']) >= 0 || !Path.IsPathFullyQualified(path)) return false;
        try
        {
            destination = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(destination);
            return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory) && !Directory.Exists(destination);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    private static bool IsSafeTemporaryPath(string destination, string? temporaryPath)
    {
        if (string.IsNullOrWhiteSpace(temporaryPath)) return false;
        try
        {
            var destinationFullPath = Path.GetFullPath(destination);
            var temporaryFullPath = Path.GetFullPath(temporaryPath);
            var destinationDirectory = Path.GetDirectoryName(destinationFullPath);
            var temporaryDirectory = Path.GetDirectoryName(temporaryFullPath);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (destinationFullPath.Equals(temporaryFullPath, comparison) || destinationDirectory is null ||
                temporaryDirectory is null || !destinationDirectory.Equals(temporaryDirectory, comparison)) return false;

            const string prefix = ".kronxy-package-";
            const string suffix = ".tmp";
            var name = Path.GetFileName(temporaryFullPath);
            if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(suffix, StringComparison.Ordinal) ||
                name.Length != prefix.Length + 32 + suffix.Length) return false;
            return name.AsSpan(prefix.Length, 32).ToString().All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f');
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    internal static CanonicalPackageManifest CreateManifest(PackageBuildRequest request,
        IReadOnlyList<PreparedPackageEntry> entries, long totalBytes)
    {
        var timestamp = request.GeneratedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
        var id = ComputePackageId(CanonicalPackageManifest.CurrentSchemaVersion,
            CanonicalPackageManifest.CurrentGeneratorVersion, request.PackageType.ToString(),
            request.Stability.ToString(), timestamp, entries);
        return new CanonicalPackageManifest
        {
            SchemaVersion = CanonicalPackageManifest.CurrentSchemaVersion,
            GeneratorVersion = CanonicalPackageManifest.CurrentGeneratorVersion,
            PackageId = id,
            PackageType = request.PackageType.ToString(), Stability = request.Stability.ToString(),
            GeneratedAtUtc = timestamp, FileCount = entries.Count, TotalContentBytes = totalBytes,
            Entries = entries.Select(entry => new CanonicalManifestEntry
            {
                Path = entry.LogicalPath, Kind = entry.Kind.ToString(), SizeBytes = entry.Content.Length, Sha256 = entry.Sha256,
                Redactions = entry.Redactions.Select(item => new CanonicalRedactionSummary
                    { Category = item.Category, Count = item.Count }).ToArray()
            }).ToArray()
        };
    }

    internal static byte[] SerializeManifest(CanonicalPackageManifest manifest) =>
        JsonSerializer.SerializeToUtf8Bytes(manifest, ManifestJson);

    internal static string ComputePackageId(string schemaVersion, string generatorVersion,
        string packageType, string stability, string timestamp, IReadOnlyList<PreparedPackageEntry> entries)
    {
        using var identity = new MemoryStream();
        WriteIdentityString(identity, schemaVersion);
        WriteIdentityString(identity, generatorVersion);
        WriteIdentityString(identity, packageType);
        WriteIdentityString(identity, stability);
        WriteIdentityString(identity, timestamp);
        WriteIdentityInt32(identity, entries.Count);
        long totalBytes = 0;
        foreach (var entry in entries)
        {
            totalBytes = checked(totalBytes + entry.Content.LongLength);
            WriteIdentityString(identity, entry.LogicalPath);
            WriteIdentityInt32(identity, (int)entry.Kind);
            WriteIdentityInt64(identity, entry.Content.LongLength);
            WriteIdentityString(identity, entry.Sha256);
            WriteIdentityInt32(identity, entry.Redactions.Count);
            foreach (var redaction in entry.Redactions)
            {
                WriteIdentityString(identity, redaction.Category);
                WriteIdentityInt32(identity, redaction.Count);
            }
        }
        WriteIdentityInt64(identity, totalBytes);
        return Hash(identity.GetBuffer().AsSpan(0, checked((int)identity.Length)));
    }

    internal static void WriteZip(Stream destination, IReadOnlyList<PreparedPackageEntry> entries,
        byte[] manifestBytes, DateTimeOffset timestamp, CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: StrictUtf8);
        var all = entries.Select(entry => (LogicalPath: entry.LogicalPath, Content: entry.Content))
            .Append((LogicalPath: PackagePath.ManifestName, Content: manifestBytes))
            .OrderBy(entry => entry.LogicalPath, StringComparer.Ordinal);
        foreach (var item in all)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var zipEntry = archive.CreateEntry(item.LogicalPath, CompressionLevel.NoCompression);
            zipEntry.LastWriteTime = NormalizeZipTimestamp(timestamp);
            zipEntry.ExternalAttributes = 0;
            using var stream = zipEntry.Open();
            for (var offset = 0; offset < item.Content.Length;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = Math.Min(16_384, item.Content.Length - offset);
                stream.Write(item.Content, offset, count);
                offset += count;
            }
        }
    }

    internal static DateTimeOffset NormalizeZipTimestamp(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        if (utc.Year is < 1980 or > 2107) throw new ArgumentOutOfRangeException(nameof(value));
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second / 2 * 2, TimeSpan.Zero);
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private async Task<(PackageBuildStatus Status, string? Hash, long? Length)> ValidatePublishedPackageAsync(
        string destination, IReadOnlyList<PreparedPackageEntry> entries, byte[] manifestBytes,
        PackageBuildLimits limits, string temporaryHash, long temporaryLength)
    {
        try
        {
            await using var stream = fileSystem.OpenRead(destination);
            if (stream.Length != temporaryLength) return (PackageBuildStatus.IntegrityFailure, null, null);
            if (!await verifier.VerifyAsync(stream, entries, manifestBytes, limits, CancellationToken.None)
                .ConfigureAwait(false)) return (PackageBuildStatus.IntegrityFailure, null, null);
            stream.Position = 0;
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, CancellationToken.None)
                .ConfigureAwait(false)).ToLowerInvariant();
            if (!HashTextEquals(hash, temporaryHash)) return (PackageBuildStatus.IntegrityFailure, null, null);
            return (PackageBuildStatus.Success, hash, stream.Length);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return (PackageBuildStatus.IntegrityFailure, null, null);
        }
    }

    private static bool IsConsistentRedactionResult(string original, RedactionResult result)
    {
        if (result.Findings is null || result.Findings.Count != 0 || result.Records is null) return false;
        var changed = !string.Equals(original, result.Content, StringComparison.Ordinal);
        return result.Status switch
        {
            RedactionStatus.SuccessUnchanged => !changed && result.Records.Count == 0,
            RedactionStatus.SuccessRedacted => changed && result.Records.Count > 0,
            _ => false
        };
    }

    private static bool ExceedsRedactionRecordLimit(
        IReadOnlyList<Kronxy.Context.Models.RedactionRecord> records, int maximumRedactions)
    {
        long total = 0;
        foreach (var record in records)
        {
            if (record is null || record.MatchCount <= 0) continue;
            total = checked(total + record.MatchCount);
            if (total > maximumRedactions) return true;
        }
        return false;
    }

    private static bool HashTextEquals(string left, string right)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(left), Convert.FromHexString(right));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private void TryDeleteWithoutThrowing(string path)
    {
        try { fileSystem.DeleteSafely(path); }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException) { }
    }

    private static void WriteIdentityString(Stream destination, string value)
    {
        var bytes = StrictUtf8.GetBytes(value);
        WriteIdentityInt32(destination, bytes.Length);
        destination.Write(bytes);
    }

    private static void WriteIdentityInt32(Stream destination, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        destination.Write(bytes);
    }

    private static void WriteIdentityInt64(Stream destination, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        destination.Write(bytes);
    }

    private static bool TryCreateRedactionSummaries(IReadOnlyList<Kronxy.Context.Models.RedactionRecord>? records,
        out IReadOnlyList<PreparedRedactionSummary> summaries)
    {
        summaries = [];
        if (records is null) return false;
        try
        {
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var record in records)
            {
                if (record is null || record.MatchCount <= 0 ||
                    !Enum.TryParse<RedactionCategory>(record.Category, ignoreCase: false, out var category) ||
                    !Enum.IsDefined(category)) return false;
                var name = category.ToString();
                counts[name] = checked(counts.GetValueOrDefault(name) + record.MatchCount);
            }
            summaries = counts.Select(item => new PreparedRedactionSummary(item.Key, item.Value)).ToArray();
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static PackageBuildStatus MapRedactionFailure(RedactionStatus status) => status switch
    {
        RedactionStatus.SensitiveContentRemaining => PackageBuildStatus.SensitiveContentRemaining,
        RedactionStatus.InputLimitExceeded or RedactionStatus.OutputLimitExceeded or RedactionStatus.TooManyRedactions => PackageBuildStatus.LimitExceeded,
        _ => PackageBuildStatus.RedactionFailed
    };

    private static PackageBuildResult Failure(PackageBuildStatus status) => new() { Status = status };
}

internal interface IAtomicPackageFileSystem
{
    bool Exists(string path);
    bool ExistsSafely(string path);
    string CreateTemporaryPath(string destinationPath);
    Stream CreateNew(string path);
    Stream OpenRead(string path);
    long Length(string path);
    PackagePublishStatus Publish(string temporaryPath, string destinationPath);
    void DeleteSafely(string path);
}

internal enum PackagePublishStatus { Success, DestinationExists, IoFailure }

internal sealed class AtomicPackageFileSystem : IAtomicPackageFileSystem
{
    public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
    public bool ExistsSafely(string path) { try { return Exists(Path.GetFullPath(path)); } catch { return false; } }
    public string CreateTemporaryPath(string destinationPath) => Path.Combine(Path.GetDirectoryName(destinationPath)!, ".kronxy-package-" + Guid.NewGuid().ToString("N") + ".tmp");
    public Stream CreateNew(string path) => new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 16_384, FileOptions.SequentialScan);
    public Stream OpenRead(string path) => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16_384, FileOptions.Asynchronous | FileOptions.SequentialScan);
    public long Length(string path) => new FileInfo(path).Length;
    public PackagePublishStatus Publish(string temporaryPath, string destinationPath)
    {
        try
        {
            File.Move(temporaryPath, destinationPath, overwrite: false);
            return PackagePublishStatus.Success;
        }
        catch (IOException)
        {
            return Exists(destinationPath) ? PackagePublishStatus.DestinationExists : PackagePublishStatus.IoFailure;
        }
        catch (UnauthorizedAccessException)
        {
            return PackagePublishStatus.IoFailure;
        }
    }
    public void DeleteSafely(string path) { try { File.Delete(path); } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { } }
}

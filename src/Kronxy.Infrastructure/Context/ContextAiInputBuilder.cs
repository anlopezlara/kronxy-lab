using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kronxy.Application.Context;
using Kronxy.Context.Configuration;

namespace Kronxy.Infrastructure.Context;

public sealed class ContextAiInputBuilder :
    IContextAiInputBuilder
{
    private const string ManifestName =
        "manifest.json";

    private const int MaxEntryBytes =
        1_048_576;

    private const int MaxManifestBytes =
        262_144;

    private static readonly UTF8Encoding StrictUtf8 =
        new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    private readonly int maxEntries;

    public ContextAiInputBuilder(
        ContextOptions? contextOptions = null)
    {
        ContextOptions options =
            contextOptions ??
            new ContextOptions();

        if (options.MaxFilesPerPackage <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(contextOptions),
                "MaxFilesPerPackage must be positive.");
        }

        maxEntries =
            options.MaxFilesPerPackage;
    }

    public async Task<ContextAiInputResult> BuildAsync(
        ContextAiInputRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null ||
            request.PackageContent.IsEmpty ||
            request.MaxCharacters <= 0)
        {
            return Failure(
                ContextAiInputFailureKind.InvalidRequest,
                "CONTEXT_AI_INPUT_INVALID_REQUEST");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var source =
                new MemoryStream(
                    request.PackageContent.ToArray(),
                    writable: false);

            using var archive =
                new ZipArchive(
                    source,
                    ZipArchiveMode.Read,
                    leaveOpen: false,
                    entryNameEncoding: StrictUtf8);

            if (archive.Entries.Count <= 1 ||
                archive.Entries.Count >
                    maxEntries + 1)
            {
                return Failure(
                    ContextAiInputFailureKind.InvalidPackage,
                    "CONTEXT_AI_PACKAGE_ENTRY_COUNT_INVALID");
            }

            ZipArchiveEntry? manifestEntry =
                archive.GetEntry(ManifestName);

            if (manifestEntry is null ||
                archive.Entries.Count(
                    entry =>
                        string.Equals(
                            entry.FullName,
                            ManifestName,
                            StringComparison.Ordinal)) != 1)
            {
                return Failure(
                    ContextAiInputFailureKind.InvalidPackage,
                    "CONTEXT_AI_MANIFEST_INVALID");
            }

            byte[]? manifestBytes =
                await ReadExactlyAsync(
                    manifestEntry,
                    MaxManifestBytes,
                    cancellationToken)
                .ConfigureAwait(false);

            if (manifestBytes is null)
            {
                return Failure(
                    ContextAiInputFailureKind.InvalidPackage,
                    "CONTEXT_AI_MANIFEST_TOO_LARGE");
            }

            ManifestModel? manifest;

            try
            {
                manifest =
                    JsonSerializer.Deserialize<ManifestModel>(
                        manifestBytes);
            }
            catch (JsonException)
            {
                return Failure(
                    ContextAiInputFailureKind.InvalidPackage,
                    "CONTEXT_AI_MANIFEST_JSON_INVALID");
            }

            if (manifest is null ||
                manifest.Entries is null ||
                manifest.Entries.Count == 0 ||
                manifest.Entries.Count > maxEntries ||
                manifest.FileCount !=
                    manifest.Entries.Count)
            {
                return Failure(
                    ContextAiInputFailureKind.InvalidPackage,
                    "CONTEXT_AI_MANIFEST_CONTENT_INVALID");
            }

            string[] expectedNames =
                manifest.Entries
                    .Select(item => item.Path)
                    .Append(ManifestName)
                    .OrderBy(
                        name => name,
                        StringComparer.Ordinal)
                    .ToArray();

            string[] actualNames =
                archive.Entries
                    .Select(entry => entry.FullName)
                    .OrderBy(
                        name => name,
                        StringComparer.Ordinal)
                    .ToArray();

            if (!expectedNames.SequenceEqual(
                    actualNames,
                    StringComparer.Ordinal))
            {
                return Failure(
                    ContextAiInputFailureKind.InvalidPackage,
                    "CONTEXT_AI_PACKAGE_ENTRIES_MISMATCH");
            }

            if (actualNames
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .Count() != actualNames.Length)
            {
                return Failure(
                    ContextAiInputFailureKind.InvalidPackage,
                    "CONTEXT_AI_PACKAGE_DUPLICATE_ENTRY");
            }

            var builder =
                new StringBuilder(
                    Math.Min(
                        request.MaxCharacters,
                        64_000));

            foreach (
                ManifestEntry item in
                manifest.Entries.OrderBy(
                    entry => entry.Path,
                    StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSafeLogicalPath(item.Path) ||
                    item.SizeBytes < 0 ||
                    item.SizeBytes > MaxEntryBytes ||
                    !IsLowerSha256(item.Sha256))
                {
                    return Failure(
                        ContextAiInputFailureKind.InvalidPackage,
                        "CONTEXT_AI_ENTRY_METADATA_INVALID");
                }

                ZipArchiveEntry? entry =
                    archive.GetEntry(item.Path);

                if (entry is null)
                {
                    return Failure(
                        ContextAiInputFailureKind.InvalidPackage,
                        "CONTEXT_AI_ENTRY_MISSING");
                }

                if (entry.Length != item.SizeBytes)
                {
                    return Failure(
                        ContextAiInputFailureKind.InvalidPackage,
                        "CONTEXT_AI_ENTRY_LENGTH_MISMATCH");
                }

                byte[]? bytes =
                    await ReadExactlyAsync(
                        entry,
                        MaxEntryBytes,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (bytes is null)
                {
                    return Failure(
                        ContextAiInputFailureKind.EntryTooLarge,
                        "CONTEXT_AI_ENTRY_TOO_LARGE");
                }

                string hash =
                    Convert.ToHexString(
                        SHA256.HashData(bytes))
                    .ToLowerInvariant();

                if (!FixedTimeHashEquals(
                        item.Sha256,
                        hash))
                {
                    return Failure(
                        ContextAiInputFailureKind.InvalidPackage,
                        "CONTEXT_AI_ENTRY_HASH_MISMATCH");
                }

                string text;

                try
                {
                    text =
                        StrictUtf8.GetString(bytes);
                }
                catch (DecoderFallbackException)
                {
                    return Failure(
                        ContextAiInputFailureKind.InvalidEncoding,
                        "CONTEXT_AI_ENTRY_UTF8_INVALID");
                }

                string header =
                    "===== FILE: " +
                    item.Path +
                    " =====\n";

                long projected =
                    (long)builder.Length +
                    header.Length +
                    text.Length +
                    2;

                if (projected >
                    request.MaxCharacters)
                {
                    continue;
                }

                builder.Append(header);
                builder.Append(text);

                if (!text.EndsWith(
                        '\n'))
                {
                    builder.Append('\n');
                }

                builder.Append('\n');
            }

            if (builder.Length == 0)
            {
                return Failure(
                    ContextAiInputFailureKind.ContentTooLarge,
                    "CONTEXT_AI_CONTENT_TOO_LARGE");
            }

            return ContextAiInputResult.Success(
                builder.ToString());
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            return Failure(
                ContextAiInputFailureKind.Cancelled,
                "CONTEXT_AI_INPUT_CANCELLED");
        }
        catch (InvalidDataException)
        {
            return Failure(
                ContextAiInputFailureKind.InvalidPackage,
                "CONTEXT_AI_ZIP_INVALID");
        }
        catch (IOException)
        {
            return Failure(
                ContextAiInputFailureKind.InvalidPackage,
                "CONTEXT_AI_ZIP_IO_FAILURE");
        }
        catch (Exception exception)
            when (exception is not
                OutOfMemoryException and not
                StackOverflowException)
        {
            return Failure(
                ContextAiInputFailureKind.InternalFailure,
                "CONTEXT_AI_INPUT_INTERNAL_FAILURE");
        }
    }

    private static async Task<byte[]?> ReadExactlyAsync(
        ZipArchiveEntry entry,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (entry.Length < 0 ||
            entry.Length > maximumBytes ||
            entry.Length > int.MaxValue)
        {
            return null;
        }

        byte[] content =
            new byte[(int)entry.Length];

        await using Stream stream =
            entry.Open();

        int offset = 0;

        while (offset < content.Length)
        {
            int read =
                await stream.ReadAsync(
                        content.AsMemory(offset),
                        cancellationToken)
                    .ConfigureAwait(false);

            if (read == 0)
            {
                break;
            }

            offset += read;
        }

        if (offset != content.Length)
        {
            return null;
        }

        int extra =
            await stream.ReadAsync(
                    new byte[1],
                    cancellationToken)
                .ConfigureAwait(false);

        return extra == 0
            ? content
            : null;
    }

    private static bool IsSafeLogicalPath(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            path == ManifestName ||
            path.StartsWith(
                "/",
                StringComparison.Ordinal) ||
            path.Contains('\\') ||
            path.Contains(
                '\0'))
        {
            return false;
        }

        return path
            .Split('/')
            .All(
                segment =>
                    segment.Length > 0 &&
                    segment is not "." and not "..");
    }

    private static bool IsLowerSha256(
        string value)
    {
        if (value is null ||
            value.Length != 64)
        {
            return false;
        }

        return value.All(
            character =>
                character is >= '0' and <= '9' or
                >= 'a' and <= 'f');
    }

    private static bool FixedTimeHashEquals(
        string left,
        string right)
    {
        try
        {
            return CryptographicOperations
                .FixedTimeEquals(
                    Convert.FromHexString(left),
                    Convert.FromHexString(right));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static ContextAiInputResult Failure(
        ContextAiInputFailureKind kind,
        string errorCode) =>
        ContextAiInputResult.Failure(
            kind,
            errorCode);

    private sealed record ManifestModel
    {
        [JsonPropertyName("fileCount")]
        public int FileCount { get; init; }

        [JsonPropertyName("entries")]
        public List<ManifestEntry> Entries { get; init; } =
            [];
    }

    private sealed record ManifestEntry
    {
        [JsonPropertyName("path")]
        public string Path { get; init; } =
            string.Empty;

        [JsonPropertyName("kind")]
        public string Kind { get; init; } =
            string.Empty;

        [JsonPropertyName("sizeBytes")]
        public long SizeBytes { get; init; }

        [JsonPropertyName("sha256")]
        public string Sha256 { get; init; } =
            string.Empty;
    }
}

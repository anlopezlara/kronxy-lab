using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Kronxy.Context.Models;
using Kronxy.Context.Redaction;

namespace Kronxy.Context.Packaging;

internal interface IPackageIntegrityVerifier
{
    Task<bool> VerifyAsync(Stream package, IReadOnlyList<PreparedPackageEntry> expectedEntries,
        byte[] expectedManifest, PackageBuildLimits limits, CancellationToken cancellationToken);
}

internal sealed class PackageIntegrityVerifier : IPackageIntegrityVerifier
{
    public async Task<bool> VerifyAsync(string packagePath, IReadOnlyList<PreparedPackageEntry> expectedEntries,
        byte[] expectedManifest, PackageBuildLimits limits, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                16_384, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return await VerifyAsync(stream, expectedEntries, expectedManifest, limits, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    public async Task<bool> VerifyAsync(Stream package, IReadOnlyList<PreparedPackageEntry> expectedEntries,
        byte[] expectedManifest, PackageBuildLimits limits, CancellationToken cancellationToken)
    {
        try
        {
            if (package is null || !package.CanRead || !package.CanSeek || package.Length <= 0 ||
                package.Length > limits.MaxPackageBytes) return false;
            package.Position = 0;
            CanonicalPackageManifest manifest;
            using (var archive = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true))
            {
                if (expectedEntries.Count > limits.MaxEntries || !ExpectedEntriesAreCanonical(expectedEntries, limits)) return false;
                var expectedNames = expectedEntries.Select(entry => entry.LogicalPath).Append(PackagePath.ManifestName)
                    .OrderBy(name => name, StringComparer.Ordinal).ToArray();
                var actualNames = archive.Entries.Select(entry => entry.FullName).ToArray();
                if (!expectedNames.SequenceEqual(actualNames, StringComparer.Ordinal) ||
                    actualNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != actualNames.Length) return false;
                foreach (var name in actualNames)
                {
                    if (name == PackagePath.ManifestName) continue;
                    if (!PackagePath.TryCanonicalize(name, limits.MaxPathBytes, out var canonical) || canonical != name) return false;
                }

                var manifestEntry = archive.GetEntry(PackagePath.ManifestName);
                if (manifestEntry is null || archive.Entries.Count(entry => entry.FullName.Equals(PackagePath.ManifestName, StringComparison.Ordinal)) != 1) return false;
                var manifestBytes = await ReadExactlyAsync(manifestEntry, limits.MaxManifestBytes, cancellationToken).ConfigureAwait(false);
                if (manifestBytes is null || !manifestBytes.AsSpan().SequenceEqual(expectedManifest)) return false;
                manifest = JsonSerializer.Deserialize<CanonicalPackageManifest>(manifestBytes)!;
                if (manifest is null || !PackageBuilder.SerializeManifest(manifest).AsSpan().SequenceEqual(manifestBytes) ||
                    !ValidateManifest(manifest, expectedEntries, limits)) return false;

                foreach (var expected in expectedEntries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var zipEntry = archive.GetEntry(expected.LogicalPath);
                    var manifestFile = manifest.Entries.SingleOrDefault(entry => entry.Path == expected.LogicalPath);
                    if (zipEntry is null || manifestFile is null || manifestFile.SizeBytes != expected.Content.Length ||
                        manifestFile.Kind != expected.Kind.ToString() || !HashEquals(manifestFile.Sha256, expected.Sha256)) return false;
                    var bytes = await ReadExactlyAsync(zipEntry, limits.MaxEntryBytes, cancellationToken).ConfigureAwait(false);
                    if (bytes is null || bytes.Length != expected.Content.Length ||
                        !bytes.AsSpan().SequenceEqual(expected.Content) || !HashEquals(PackageBuilder.Hash(bytes), expected.Sha256)) return false;
                }
            }

            return await MatchesCanonicalArchiveAsync(package, expectedEntries, expectedManifest,
                manifest.GeneratedAtUtc, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is InvalidDataException or IOException or JsonException or
            NotSupportedException or ArgumentException or OverflowException)
        {
            return false;
        }
    }

    private static bool ExpectedEntriesAreCanonical(IReadOnlyList<PreparedPackageEntry> entries, PackageBuildLimits limits)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? previous = null;
        long totalBytes = 0;
        foreach (var entry in entries)
        {
            if (entry.Content is null || entry.Content.Length > limits.MaxEntryBytes || !IsLowerSha256(entry.Sha256) ||
                !PackagePath.TryCanonicalize(entry.LogicalPath, limits.MaxPathBytes, out var canonical) ||
                canonical != entry.LogicalPath || !paths.Add(canonical) ||
                previous is not null && StringComparer.Ordinal.Compare(previous, canonical) >= 0) return false;
            totalBytes = checked(totalBytes + entry.Content.LongLength);
            if (totalBytes > limits.MaxTotalContentBytes) return false;
            previous = canonical;
        }
        return true;
    }

    private static bool ValidateManifest(CanonicalPackageManifest manifest,
        IReadOnlyList<PreparedPackageEntry> expectedEntries, PackageBuildLimits limits)
    {
        if (manifest.SchemaVersion != CanonicalPackageManifest.CurrentSchemaVersion ||
            manifest.GeneratorVersion != CanonicalPackageManifest.CurrentGeneratorVersion ||
            manifest.FileCount != expectedEntries.Count || manifest.Entries is null ||
            manifest.Entries.Count != expectedEntries.Count || !IsLowerSha256(manifest.PackageId) ||
            !Enum.GetNames<PackageType>().Contains(manifest.PackageType, StringComparer.Ordinal) ||
            !Enum.GetNames<PackageStability>().Contains(manifest.Stability, StringComparer.Ordinal) ||
            !DateTimeOffset.TryParseExact(manifest.GeneratedAtUtc, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var generatedAt)) return false;

        long totalBytes = 0;
        try
        {
            foreach (var entry in expectedEntries) totalBytes = checked(totalBytes + entry.Content.LongLength);
        }
        catch (OverflowException)
        {
            return false;
        }
        if (manifest.TotalContentBytes != totalBytes || totalBytes > limits.MaxTotalContentBytes ||
            !manifest.Entries.Select(entry => entry.Path).SequenceEqual(
                expectedEntries.Select(entry => entry.LogicalPath), StringComparer.Ordinal)) return false;

        for (var index = 0; index < expectedEntries.Count; index++)
        {
            var expected = expectedEntries[index];
            var actual = manifest.Entries[index];
            if (actual.Path != expected.LogicalPath || actual.Kind != expected.Kind.ToString() ||
                actual.SizeBytes != expected.Content.Length || !HashEquals(actual.Sha256, expected.Sha256) ||
                actual.Redactions is null || actual.Redactions.Count != expected.Redactions.Count ||
                !actual.Redactions.Select(item => item.Category).SequenceEqual(
                    expected.Redactions.Select(item => item.Category), StringComparer.Ordinal)) return false;

            for (var redactionIndex = 0; redactionIndex < expected.Redactions.Count; redactionIndex++)
            {
                var expectedRedaction = expected.Redactions[redactionIndex];
                var actualRedaction = actual.Redactions[redactionIndex];
                if (actualRedaction.Count != expectedRedaction.Count || actualRedaction.Count <= 0 ||
                    !Enum.GetNames<RedactionCategory>().Contains(actualRedaction.Category, StringComparer.Ordinal)) return false;
            }
        }

        var packageId = PackageBuilder.ComputePackageId(manifest.SchemaVersion, manifest.GeneratorVersion,
            manifest.PackageType, manifest.Stability, manifest.GeneratedAtUtc, expectedEntries);
        return HashEquals(manifest.PackageId, packageId);
    }

    private static async Task<bool> MatchesCanonicalArchiveAsync(Stream actual,
        IReadOnlyList<PreparedPackageEntry> expectedEntries, byte[] expectedManifest,
        string generatedAtUtc, CancellationToken cancellationToken)
    {
        if (!DateTimeOffset.TryParseExact(generatedAtUtc, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var timestamp)) return false;

        using var expected = new MemoryStream();
        PackageBuilder.WriteZip(expected, expectedEntries, expectedManifest, timestamp, cancellationToken);
        if (actual.Length != expected.Length) return false;
        actual.Position = 0;
        expected.Position = 0;
        var actualBuffer = new byte[16_384];
        var expectedBuffer = new byte[16_384];
        while (true)
        {
            var actualRead = await actual.ReadAsync(actualBuffer, cancellationToken).ConfigureAwait(false);
            var expectedRead = await expected.ReadAsync(expectedBuffer, cancellationToken).ConfigureAwait(false);
            if (actualRead != expectedRead) return false;
            if (actualRead == 0) return true;
            if (!actualBuffer.AsSpan(0, actualRead).SequenceEqual(expectedBuffer.AsSpan(0, expectedRead))) return false;
        }
    }

    private static bool HashEquals(string? left, string? right)
    {
        if (!TryDecodeHash(left, out var leftBytes) || !TryDecodeHash(right, out var rightBytes)) return false;
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static bool IsLowerSha256(string? value) => TryDecodeHash(value, out _);

    private static bool TryDecodeHash(string? value, out byte[] bytes)
    {
        bytes = [];
        if (value is null || value.Length != 64 || value.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))) return false;
        try
        {
            bytes = Convert.FromHexString(value);
            return bytes.Length == SHA256.HashSizeInBytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static async Task<byte[]?> ReadExactlyAsync(ZipArchiveEntry entry, int maximumBytes, CancellationToken cancellationToken)
    {
        if (entry.Length < 0 || entry.Length > maximumBytes) return null;
        await using var source = entry.Open();
        using var destination = new MemoryStream((int)entry.Length);
        var buffer = new byte[16_384];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            total = checked(total + read);
            if (total > maximumBytes) return null;
            destination.Write(buffer, 0, read);
        }
        return destination.ToArray();
    }
}

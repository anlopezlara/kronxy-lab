using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kronxy.Application.Execution;

namespace Kronxy.Infrastructure.Execution;

public interface IPlanningPriorityPathSelector
{
    IReadOnlyList<string> Select(
        ReadOnlyMemory<byte> packageContent,
        string jobRequest);
}

public sealed class PlanningPriorityPathSelector :
    IPlanningPriorityPathSelector
{
    private const string ManifestName =
        "manifest.json";

    private const int MaxPriorityPaths = 24;

    private static readonly HashSet<string> StopWords =
        new(
            [
                "a",
                "an",
                "and",
                "are",
                "be",
                "by",
                "for",
                "in",
                "is",
                "it",
                "make",
                "of",
                "on",
                "only",
                "or",
                "so",
                "the",
                "to",
                "while",
                "with",
                "add",
                "required",
                "necessary",
                "complete",
                "smallest",
                "accepted",
                "rejected",
                "remain",
                "focused",
                "automated",
                "demonstrate",
                "behavior",
                "change",
                "test",
                "tests",
                "valid"
            ],
            StringComparer.Ordinal);

    public IReadOnlyList<string> Select(
        ReadOnlyMemory<byte> packageContent,
        string jobRequest)
    {
        if (string.IsNullOrWhiteSpace(
                jobRequest))
        {
            return Array.Empty<string>();
        }

        using var stream =
            new MemoryStream(
                packageContent.ToArray(),
                writable: false);

        using var archive =
            new ZipArchive(
                stream,
                ZipArchiveMode.Read,
                leaveOpen: false);

        ZipArchiveEntry? manifestEntry =
            archive.GetEntry(
                ManifestName);

        if (manifestEntry is null)
        {
            return Array.Empty<string>();
        }

        ManifestModel? manifest;

        using (
            Stream manifestStream =
                manifestEntry.Open())
        {
            manifest =
                JsonSerializer.Deserialize<ManifestModel>(
                    manifestStream);
        }

        if (manifest?.Entries is null)
        {
            return Array.Empty<string>();
        }

        HashSet<string> requestTokens =
            Tokenize(
                jobRequest);

        bool prioritizeTests =
            RequestsAutomatedTests(
                jobRequest);

        IReadOnlyList<HashSet<string>> explicitReferences =
            ExtractExplicitReferenceTerms(
                jobRequest);

        if (requestTokens.Count == 0)
        {
            return Array.Empty<string>();
        }

        string? explicitLayerPrefix =
            ResolveExplicitLayerPrefix(
                jobRequest);

        IEnumerable<ManifestEntry> eligibleEntries =
            manifest.Entries
                .Where(
                    entry =>
                        !string.IsNullOrWhiteSpace(
                            entry.Path));

        if (!string.IsNullOrWhiteSpace(
                explicitLayerPrefix))
        {
            eligibleEntries =
                eligibleEntries.Where(
                    entry =>
                        entry.Path.StartsWith(
                            explicitLayerPrefix,
                            StringComparison.Ordinal));
        }

        return eligibleEntries
            .Select(
                entry =>
                    new
                    {
                        entry.Path,
                        entry.SizeBytes,
                        Score =
                            ScorePath(
                                entry.Path,
                                requestTokens,
                                prioritizeTests,
                                jobRequest,
                                explicitReferences)
                    })
            .Where(
                value =>
                    value.Score > 0)
            .OrderByDescending(
                value =>
                    value.Score)
            .ThenBy(
                value =>
                    value.SizeBytes)
            .ThenBy(
                value =>
                    value.Path,
                StringComparer.Ordinal)
            .Take(
                MaxPriorityPaths)
            .Select(
                value =>
                    value.Path)
            .ToArray();
    }

    internal static IReadOnlySet<string> ExtractManifestPaths(
        ReadOnlyMemory<byte> packageContent)
    {
        try
        {
            using var stream = new MemoryStream(
                packageContent.ToArray(),
                writable: false);
            using var archive = new ZipArchive(
                stream,
                ZipArchiveMode.Read,
                leaveOpen: false);
            ZipArchiveEntry? manifestEntry = archive.GetEntry(ManifestName);
            if (manifestEntry is null)
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            using Stream manifestStream = manifestEntry.Open();
            ManifestModel? manifest = JsonSerializer.Deserialize<ManifestModel>(
                manifestStream);

            return manifest?.Entries?
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Path))
                .Select(entry => entry.Path)
                .ToHashSet(StringComparer.Ordinal) ??
                new HashSet<string>(StringComparer.Ordinal);
        }
        catch (InvalidDataException)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    internal static bool HasTopFivePlanPathOverlap(
        PlannerPlan? plan,
        IReadOnlyList<string>? priorityPaths)
    {
        if (plan is null ||
            priorityPaths is null ||
            priorityPaths.Count == 0 ||
            plan.FilesToInspect is null ||
            plan.CandidateFilesToModify is null)
        {
            return false;
        }

        HashSet<string> topFive =
            priorityPaths
                .Take(5)
                .ToHashSet(StringComparer.Ordinal);

        return plan.FilesToInspect
            .Concat(plan.CandidateFilesToModify)
            .Any(topFive.Contains);
    }

    internal static string? ResolveExplicitLayerPrefix(
        string jobRequest)
    {
        if (string.IsNullOrWhiteSpace(
                jobRequest))
        {
            return null;
        }

        string normalized =
            jobRequest.ToLowerInvariant();

        if (normalized.Contains(
                "domain-only",
                StringComparison.Ordinal) ||
            normalized.Contains(
                "domain only",
                StringComparison.Ordinal) ||
            normalized.Contains(
                "domain layer",
                StringComparison.Ordinal) ||
            normalized.Contains(
                "limited to domain code",
                StringComparison.Ordinal))
        {
            return "src/Kronxy.Domain/";
        }

        return null;
    }

    internal static HashSet<string> Tokenize(
        string value)
    {
        var result =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (
            string token
            in SplitTokens(value))
        {
            string normalized =
                token.ToLowerInvariant();

            if (normalized.Length >= 2 &&
                !StopWords.Contains(
                    normalized))
            {
                result.Add(
                    normalized);
            }
        }

        return result;
    }

    private static int ScorePath(
        string path,
        HashSet<string> requestTokens,
        bool prioritizeTests,
        string jobRequest,
        IReadOnlyList<HashSet<string>> explicitReferences)
    {
        HashSet<string> pathTokens =
            Tokenize(
                SplitCamelCase(path));

        int shared = 0;

        foreach (
            string token
            in requestTokens)
        {
            if (pathTokens.Contains(
                    token))
            {
                shared++;
            }
        }

        if (shared == 0)
        {
            return 0;
        }

        int score =
            shared * 10;

        if (jobRequest.Contains(
                path,
                StringComparison.Ordinal))
        {
            score += 10_000;
        }

        if (explicitReferences.Any(reference =>
                reference.IsSubsetOf(pathTokens)))
        {
            score += 20_000;
        }

        if (prioritizeTests &&
            IsTestPath(path))
        {
            score++;
        }

        return score;
    }

    internal static IReadOnlyList<HashSet<string>>
        ExtractExplicitReferenceTerms(
            string jobRequest)
    {
        var references =
            new List<HashSet<string>>();

        int searchFrom = 0;

        while (searchFrom < jobRequest.Length)
        {
            int useIndex = jobRequest.IndexOf(
                "use ",
                searchFrom,
                StringComparison.OrdinalIgnoreCase);

            if (useIndex < 0)
            {
                break;
            }

            int valueStart = useIndex + 4;
            int asIndex = jobRequest.IndexOf(
                " as ",
                valueStart,
                StringComparison.OrdinalIgnoreCase);

            if (asIndex < 0)
            {
                break;
            }

            string? identifier =
                SplitTokens(
                    jobRequest[valueStart..asIndex])
                    .LastOrDefault();

            if (!string.IsNullOrWhiteSpace(identifier))
            {
                HashSet<string> tokens =
                    Tokenize(
                        SplitCamelCase(identifier));

                if (tokens.Count > 0)
                {
                    references.Add(tokens);
                }
            }

            searchFrom = asIndex + 4;
        }

        return references;
    }

    private static bool IsTestPath(
        string path) =>
        path.StartsWith(
            "tests/",
            StringComparison.Ordinal);

    private static bool RequestsAutomatedTests(
        string value)
    {
        HashSet<string> tokens =
            TokenizeWithoutStopWords(
                value);

        return
            tokens.Contains("test") ||
            tokens.Contains("tests") ||
            tokens.Contains("automated");
    }

    private static HashSet<string> TokenizeWithoutStopWords(
        string value)
    {
        var result =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (
            string token
            in SplitTokens(value))
        {
            string normalized =
                token.ToLowerInvariant();

            if (normalized.Length >= 2)
            {
                result.Add(
                    normalized);
            }
        }

        return result;
    }

    private static IEnumerable<string> SplitTokens(
        string value)
    {
        var current =
            new List<char>();

        foreach (
            char character
            in value)
        {
            if (char.IsLetterOrDigit(
                    character))
            {
                current.Add(
                    character);
            }
            else if (current.Count > 0)
            {
                yield return new string(
                    current.ToArray());

                current.Clear();
            }
        }

        if (current.Count > 0)
        {
            yield return new string(
                current.ToArray());
        }
    }

    private static string SplitCamelCase(
        string value)
    {
        var result =
            new System.Text.StringBuilder(
                value.Length + 16);

        char previous = '\0';

        foreach (
            char current
            in value)
        {
            if (previous != '\0' &&
                char.IsLower(previous) &&
                char.IsUpper(current))
            {
                result.Append(' ');
            }

            result.Append(current);
            previous = current;
        }

        return result.ToString();
    }

    private sealed record ManifestModel
    {
        [JsonPropertyName("entries")]
        public List<ManifestEntry> Entries
        {
            get;
            init;
        } = [];
    }

    private sealed record ManifestEntry
    {
        [JsonPropertyName("path")]
        public string Path
        {
            get;
            init;
        } = string.Empty;

        [JsonPropertyName("sizeBytes")]
        public long SizeBytes
        {
            get;
            init;
        }
    }
}

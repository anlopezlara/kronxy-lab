namespace Kronxy.Infrastructure.Execution;

public sealed record DeveloperChangePolicyOptions
{
    public const string SectionName =
        "DeveloperPolicy";

    public int MaxOperations { get; init; } = 4;

    public int MaxCreatedFiles { get; init; } = 3;

    public int MaxFileBytes { get; init; } =
        262_144;

    public int MaxTotalChangeBytes { get; init; } =
        1_048_576;

    public int MaxProposalBytes { get; init; } =
        2_097_152;

    public int MaxPathDepth { get; init; } = 16;

    public int MaxPathCharacters { get; init; } = 240;

    public int MaxSummaryCharacters { get; init; } = 256;

    public int MaxIntentCharacters { get; init; } = 256;

    public int MaxMetadataItems { get; init; } = 4;

    public int MaxMetadataItemCharacters { get; init; } = 256;

    public IReadOnlyList<string> ProtectedPathPrefixes
    {
        get;
        init;
    } =
    [
        ".git",
        ".github/workflows"
    ];

    public IReadOnlyList<string> ProtectedFileNames
    {
        get;
        init;
    } =
    [
        ".env",
        ".kronxy-workspace.json",
        "AGENTS.md",
        "Directory.Build.props",
        "Directory.Build.targets",
        "global.json",
        "NuGet.config"
    ];

    public IReadOnlyList<string> ProtectedFileNamePrefixes
    {
        get;
        init;
    } =
    [
        ".env.",
        "appsettings."
    ];

    public void Validate()
    {
        if (MaxOperations <= 0)
        {
            throw new InvalidOperationException(
                "DeveloperPolicy MaxOperations must be positive.");
        }

        if (MaxCreatedFiles <= 0 ||
            MaxCreatedFiles > MaxOperations)
        {
            throw new InvalidOperationException(
                "DeveloperPolicy MaxCreatedFiles is invalid.");
        }

        if (MaxFileBytes <= 0)
        {
            throw new InvalidOperationException(
                "DeveloperPolicy MaxFileBytes must be positive.");
        }

        if (MaxTotalChangeBytes < MaxFileBytes)
        {
            throw new InvalidOperationException(
                "DeveloperPolicy MaxTotalChangeBytes is invalid.");
        }

        if (MaxProposalBytes < MaxTotalChangeBytes)
        {
            throw new InvalidOperationException(
                "DeveloperPolicy MaxProposalBytes is invalid.");
        }

        if (MaxPathDepth <= 0)
        {
            throw new InvalidOperationException(
                "DeveloperPolicy MaxPathDepth must be positive.");
        }

        if (MaxPathCharacters <= 0)
        {
            throw new InvalidOperationException(
                "DeveloperPolicy MaxPathCharacters must be positive.");
        }

        if (MaxSummaryCharacters <= 0 || MaxIntentCharacters <= 0 ||
            MaxMetadataItems <= 0 || MaxMetadataItemCharacters <= 0)
        {
            throw new InvalidOperationException(
                "DeveloperPolicy metadata limits must be positive.");
        }

        ValidatePathPrefixes(
            ProtectedPathPrefixes);

        ValidateFileNames(
            ProtectedFileNames,
            nameof(ProtectedFileNames));

        ValidateFileNames(
            ProtectedFileNamePrefixes,
            nameof(ProtectedFileNamePrefixes));
    }

    private static void ValidatePathPrefixes(
        IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            throw new InvalidOperationException(
                "DeveloperPolicy ProtectedPathPrefixes is invalid.");
        }

        foreach (string value in values)
        {
            if (!IsSafeConfiguredPath(value))
            {
                throw new InvalidOperationException(
                    "DeveloperPolicy ProtectedPathPrefixes is invalid.");
            }
        }

        if (values.Count !=
            values.Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Count())
        {
            throw new InvalidOperationException(
                "DeveloperPolicy ProtectedPathPrefixes contains duplicates.");
        }
    }

    private static void ValidateFileNames(
        IReadOnlyList<string>? values,
        string name)
    {
        if (values is null)
        {
            throw new InvalidOperationException(
                $"DeveloperPolicy {name} is invalid.");
        }

        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.IndexOfAny(
                    ['\0', '\r', '\n', '/', '\\']) >= 0)
            {
                throw new InvalidOperationException(
                    $"DeveloperPolicy {name} is invalid.");
            }
        }

        if (values.Count !=
            values.Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Count())
        {
            throw new InvalidOperationException(
                $"DeveloperPolicy {name} contains duplicates.");
        }
    }

    private static bool IsSafeConfiguredPath(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.IndexOfAny(
                ['\0', '\r', '\n', '\\']) >= 0 ||
            Path.IsPathFullyQualified(value) ||
            value.StartsWith('/'))
        {
            return false;
        }

        string[] segments =
            value.Split('/');

        return segments.All(
            segment =>
                !string.IsNullOrWhiteSpace(segment) &&
                segment is not "." and not "..");
    }
}

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Kronxy.Application.Execution;

namespace Kronxy.Infrastructure.Execution;

public sealed class DeveloperProposalPolicy :
    IDeveloperProposalPolicy
{
    private static readonly UTF8Encoding StrictUtf8 =
        new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    private readonly DeveloperChangePolicyOptions options;

    public DeveloperProposalPolicy(
        DeveloperChangePolicyOptions options)
    {
        this.options =
            options ??
            throw new ArgumentNullException(
                nameof(options));

        this.options.Validate();
    }

    public DeveloperProposalPolicyResult Validate(
        DeveloperProposal? proposal)
    {
        if (!HasValidProposalShape(proposal))
        {
            return Failure(
                DeveloperProposalFailureKind.InvalidProposal,
                "DEVELOPER_PROPOSAL_INVALID");
        }

        if (proposal.Changes.Count >
            options.MaxOperations)
        {
            return Failure(
                DeveloperProposalFailureKind
                    .TooManyOperations,
                "DEVELOPER_TOO_MANY_OPERATIONS");
        }

        int proposalBytes;

        try
        {
            proposalBytes =
                JsonSerializer.SerializeToUtf8Bytes(
                        proposal)
                    .Length;
        }
        catch (Exception exception)
            when (exception is JsonException or
                  NotSupportedException or
                  ArgumentException or
                  EncoderFallbackException)
        {
            return Failure(
                DeveloperProposalFailureKind.InvalidProposal,
                "DEVELOPER_PROPOSAL_INVALID");
        }

        if (proposalBytes >
            options.MaxProposalBytes)
        {
            return Failure(
                DeveloperProposalFailureKind.ProposalTooLarge,
                "DEVELOPER_PROPOSAL_TOO_LARGE");
        }

        var validated =
            new List<ValidatedDeveloperChange>(
                proposal.Changes.Count);

        var paths =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        int createdFiles = 0;
        long totalChangeBytes = 0;

        foreach (
            DeveloperChangeOperation? change
            in proposal.Changes)
        {
            if (change is null ||
                change.Content is null ||
                change.ExpectedContentSha256 is null)
            {
                return Failure(
                    DeveloperProposalFailureKind.InvalidProposal,
                    "DEVELOPER_PROPOSAL_INVALID");
            }

            if (!Enum.IsDefined(
                    change.Operation) ||
                change.Operation is not (
                    DeveloperChangeOperationType.CreateFile or
                    DeveloperChangeOperationType.ReplaceFile))
            {
                return Failure(
                    DeveloperProposalFailureKind
                        .UnsupportedOperation,
                    "DEVELOPER_OPERATION_UNSUPPORTED");
            }

            if (!TryNormalizeRelativePath(
                    change.RelativePath,
                    out string normalizedPath))
            {
                return Failure(
                    DeveloperProposalFailureKind.InvalidPath,
                    "DEVELOPER_PATH_INVALID");
            }

            if (IsProtectedPath(
                    normalizedPath))
            {
                return Failure(
                    DeveloperProposalFailureKind.ProtectedPath,
                    "DEVELOPER_PATH_PROTECTED");
            }

            if (!paths.Add(
                    normalizedPath))
            {
                return Failure(
                    DeveloperProposalFailureKind.DuplicatePath,
                    "DEVELOPER_PATH_DUPLICATE");
            }

            if (change.Intent is null ||
                change.Intent.Length > options.MaxIntentCharacters ||
                !IsValidText(
                    change.Intent,
                    requireContent: true))
            {
                return Failure(
                    DeveloperProposalFailureKind.InvalidIntent,
                    "DEVELOPER_INTENT_INVALID");
            }

            if (!TryValidateExpectedHash(
                    change,
                    out string normalizedHash))
            {
                return Failure(
                    DeveloperProposalFailureKind
                        .InvalidExpectedHash,
                    "DEVELOPER_EXPECTED_HASH_INVALID");
            }

            int contentBytes;

            try
            {
                contentBytes =
                    StrictUtf8.GetByteCount(
                        change.Content);
            }
            catch (EncoderFallbackException)
            {
                return Failure(
                    DeveloperProposalFailureKind.InvalidProposal,
                    "DEVELOPER_PROPOSAL_INVALID");
            }

            if (contentBytes >
                options.MaxFileBytes)
            {
                return Failure(
                    DeveloperProposalFailureKind.FileTooLarge,
                    "DEVELOPER_FILE_TOO_LARGE");
            }

            totalChangeBytes +=
                contentBytes;

            if (totalChangeBytes >
                options.MaxTotalChangeBytes)
            {
                return Failure(
                    DeveloperProposalFailureKind
                        .TotalChangeBytesExceeded,
                    "DEVELOPER_TOTAL_BYTES_EXCEEDED");
            }

            if (change.Operation ==
                DeveloperChangeOperationType.CreateFile)
            {
                createdFiles++;

                if (createdFiles >
                    options.MaxCreatedFiles)
                {
                    return Failure(
                        DeveloperProposalFailureKind
                            .TooManyCreatedFiles,
                        "DEVELOPER_TOO_MANY_CREATED_FILES");
                }
            }

            validated.Add(
                new ValidatedDeveloperChange(
                    change.Operation,
                    normalizedPath,
                    change.Intent,
                    change.Content,
                    normalizedHash,
                    contentBytes));
        }

        return DeveloperProposalPolicyResult.Success(
            new ValidatedDeveloperProposal(
                proposal.Summary,
                Array.AsReadOnly(
                    validated.ToArray()),
                Array.AsReadOnly(
                    proposal.Assumptions.ToArray()),
                Array.AsReadOnly(
                    proposal.Risks.ToArray()),
                totalChangeBytes,
                proposalBytes));
    }

    private bool HasValidProposalShape(
        [NotNullWhen(true)]
        DeveloperProposal? proposal)
    {
        if (proposal is null ||
            proposal.Changes is null ||
            proposal.Assumptions is null ||
            proposal.Risks is null ||
            proposal.Changes.Count == 0 ||
            proposal.Summary is null ||
            proposal.Summary.Length > options.MaxSummaryCharacters ||
            proposal.Assumptions.Count > options.MaxMetadataItems ||
            proposal.Risks.Count > options.MaxMetadataItems ||
            !IsValidText(
                proposal.Summary,
                requireContent: true))
        {
            return false;
        }

        return proposal.Assumptions.All(
                   value => value is not null &&
                       value.Length <= options.MaxMetadataItemCharacters &&
                       IsValidText(value, requireContent: true)) &&
               proposal.Risks.All(
                   value => value is not null &&
                       value.Length <= options.MaxMetadataItemCharacters &&
                       IsValidText(value, requireContent: true));
    }

    private bool TryNormalizeRelativePath(
        string? value,
        out string normalized)
    {
        normalized =
            string.Empty;

        if (string.IsNullOrWhiteSpace(value) ||
            value.Length >
                options.MaxPathCharacters ||
            !string.Equals(
                value,
                value.Trim(),
                StringComparison.Ordinal) ||
            value.IndexOfAny(
                [
                    '\0',
                    '\r',
                    '\n',
                    '\\',
                    ':',
                    '*',
                    '?',
                    '"',
                    '<',
                    '>',
                    '|'
                ]) >= 0 ||
            Path.IsPathFullyQualified(value) ||
            value.StartsWith('/'))
        {
            return false;
        }

        string[] segments =
            value.Split('/');

        if (segments.Length >
                options.MaxPathDepth ||
            segments.Any(
                segment =>
                    string.IsNullOrWhiteSpace(segment) ||
                    segment is "." or ".." ||
                    segment.EndsWith(
                        ".",
                        StringComparison.Ordinal) ||
                    segment.EndsWith(
                        " ",
                        StringComparison.Ordinal)))
        {
            return false;
        }

        normalized =
            string.Join(
                '/',
                segments);

        return true;
    }

    private bool IsProtectedPath(
        string normalizedPath)
    {
        foreach (
            string prefix
            in options.ProtectedPathPrefixes)
        {
            if (normalizedPath.Equals(
                    prefix,
                    StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.StartsWith(
                    prefix + "/",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        string fileName =
            normalizedPath
                .Split('/')[^1];

        if (options.ProtectedFileNames.Any(
                value =>
                    fileName.Equals(
                        value,
                        StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return options.ProtectedFileNamePrefixes.Any(
            value =>
                fileName.StartsWith(
                    value,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryValidateExpectedHash(
        DeveloperChangeOperation change,
        out string normalizedHash)
    {
        normalizedHash =
            string.Empty;

        if (change.Operation ==
            DeveloperChangeOperationType.CreateFile)
        {
            return change.ExpectedContentSha256.Length == 0;
        }

        if (change.ExpectedContentSha256.Length != 64 ||
            !change.ExpectedContentSha256.All(
                Uri.IsHexDigit))
        {
            return false;
        }

        normalizedHash =
            change.ExpectedContentSha256
                .ToLowerInvariant();

        return true;
    }

    private static bool IsValidText(
        string? value,
        bool requireContent)
    {
        if (value is null ||
            value.IndexOf('\0') >= 0 ||
            requireContent &&
            string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            _ = StrictUtf8.GetByteCount(value);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    private static DeveloperProposalPolicyResult Failure(
        DeveloperProposalFailureKind kind,
        string errorCode)
    {
        return DeveloperProposalPolicyResult.Failure(
            kind,
            errorCode);
    }
}

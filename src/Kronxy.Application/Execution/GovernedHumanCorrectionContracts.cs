using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kronxy.Domain.Jobs;

namespace Kronxy.Application.Execution;

public sealed record GovernedHumanFileReplacement
{
    public required string RelativePath { get; init; }
    public required string ExpectedContentSha256 { get; init; }
    public required string Content { get; init; }
}

public sealed record GovernedHumanCorrectionRequest
{
    public required IReadOnlyList<GovernedHumanFileReplacement> Changes { get; init; }
    public string Actor { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public sealed record GovernedHumanCorrectionEvidence
{
    public required Guid JobId { get; init; }
    public required Guid RunId { get; init; }
    public required int AttemptCount { get; init; }
    public required string Stage { get; init; }
    public required string Actor { get; init; }
    public required string CorrelationId { get; init; }
    public required string Reason { get; init; }
    public required string RequestSha256 { get; init; }
    public required IReadOnlyList<string> FailureEvidence { get; init; }
    public required IReadOnlyList<string> ExhaustedAiCorrectionEvidence { get; init; }
    public required IReadOnlyList<string> AllowedPaths { get; init; }
    public required ValidatedDeveloperProposal Proposal { get; init; }
    public required DateTimeOffset RecordedAtUtc { get; init; }
}

public sealed record GovernedHumanCorrectionReceipt
{
    public required Guid JobId { get; init; }
    public required Guid RunId { get; init; }
    public required int AttemptCount { get; init; }
    public required string Actor { get; init; }
    public required string CorrelationId { get; init; }
    public required string RequestSha256 { get; init; }
    public required IReadOnlyList<AppliedFileChange> Changes { get; init; }
    public required DateTimeOffset RecordedAtUtc { get; init; }
}

public static class GovernedHumanCorrectionPolicy
{
    public static bool IsEligible(
        JobState state,
        bool isTerminal,
        bool hasValidFailureEvidence,
        bool aiCorrectionExhausted,
        IReadOnlyList<string> allowedPaths) =>
        state == JobState.Building &&
        !isTerminal &&
        hasValidFailureEvidence &&
        aiCorrectionExhausted &&
        allowedPaths is { Count: > 0 };

    public static bool IsValidRequest(
        GovernedHumanCorrectionRequest? request,
        IReadOnlyList<string> allowedPaths)
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.Actor) ||
            string.IsNullOrWhiteSpace(request.CorrelationId) ||
            request.Actor.Length > 200 ||
            request.CorrelationId.Length > 200 ||
            request.Reason.Length > 4000 ||
            request.Actor.IndexOfAny(['\0', '\r', '\n']) >= 0 ||
            request.CorrelationId.IndexOfAny(['\0', '\r', '\n']) >= 0 ||
            request.Changes is not { Count: > 0 and <= 4 })
            return false;

        HashSet<string> allowed = allowedPaths
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] paths = request.Changes
            .Select(change => change.RelativePath)
            .ToArray();
        return paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() ==
                paths.Length &&
            request.Changes.All(change =>
                SafeRelativePath(change.RelativePath) &&
                allowed.Contains(change.RelativePath) &&
                IsSha256(change.ExpectedContentSha256) &&
                !string.IsNullOrEmpty(change.Content));
    }

    public static bool MatchesCurrentSource(
        GovernedHumanFileReplacement replacement,
        string currentSha256) =>
        IsSha256(currentSha256) &&
        string.Equals(
            replacement.ExpectedContentSha256,
            currentSha256,
            StringComparison.Ordinal);

    public static bool IsNoOp(
        GovernedHumanFileReplacement replacement) =>
        string.Equals(
            replacement.ExpectedContentSha256,
            Hash(replacement.Content),
            StringComparison.Ordinal);

    public static string Fingerprint(
        GovernedHumanCorrectionRequest request) =>
        Convert.ToHexString(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                request.Actor,
                request.CorrelationId,
                request.Reason,
                Changes = request.Changes
                    .OrderBy(change => change.RelativePath, StringComparer.Ordinal)
                    .Select(change => new
                    {
                        change.RelativePath,
                        change.ExpectedContentSha256,
                        change.Content
                    })
            }))).ToLowerInvariant();

    private static string Hash(string content) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(content)))
        .ToLowerInvariant();

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool SafeRelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 512)
            return false;
        string normalized = path.Replace('\\', '/');
        return !normalized.StartsWith('/') &&
            !Path.IsPathFullyQualified(normalized) &&
            normalized.Split('/').All(segment =>
                segment.Length > 0 && segment is not "." and not "..");
    }
}

using System.Security.Cryptography;
using System.Text;
using Kronxy.Application.Execution;

namespace Kronxy.Infrastructure.Execution;

internal static class SafeChangeProposalLineage
{
    private const int MaximumLineageIdLength = 512;

    public static bool HasVersionedIdentity(
        SafeChangeApplicationRequest request) =>
        GetLineageId(request) is not null;

    public static string? GetLineageId(
        SafeChangeApplicationRequest request)
    {
        string? lineage = string.IsNullOrWhiteSpace(
                request.ProposalLineageId)
            ? DerivedLineageId(request)
            : request.ProposalLineageId;

        return IsValidLineageId(lineage)
            ? lineage
            : null;
    }

    public static string GetCompletionReceiptPath(
        SafeChangeApplicationRequest request)
    {
        string? lineageHash = GetLineageHash(request);
        return Path.Combine(
            request.Repository.WorkspacePath,
            ".kronxy",
            "change-completions",
            $"{request.JobId:N}-{request.RunId:N}" +
            (lineageHash is null
                ? LegacySuffix(request)
                : $"-lineage-{lineageHash}") +
            ".json");
    }

    public static string GetLegacyCompletionReceiptPath(
        SafeChangeApplicationRequest request) =>
        Path.Combine(
            request.Repository.WorkspacePath,
            ".kronxy",
            "change-completions",
            $"{request.JobId:N}-{request.RunId:N}" +
            LegacySuffix(request) +
            ".json");

    public static string GetTransactionPath(
        SafeChangeApplicationRequest request)
    {
        string? lineageHash = GetLineageHash(request);
        return Path.Combine(
            request.Repository.WorkspacePath,
            ".kronxy",
            "change-transactions",
            $"{request.JobId:N}-{request.RunId:N}" +
            (lineageHash is null
                ? LegacySuffix(request)
                : $"-lineage-{lineageHash}"));
    }

    private static string? DerivedLineageId(
        SafeChangeApplicationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CorrelationId))
            return null;

        string kind = request.IsGovernedHumanCorrection
            ? "governed-human-correction"
            : request.IsHumanReviewCorrection
                ? "human-review-correction"
                : request.IsBuildCorrectionRetry
                    ? "build-correction-retry"
                    : request.IsBuildCorrection
                        ? "build-correction"
                        : "developer";
        return $"{kind}:{request.CorrelationId}";
    }

    private static string? GetLineageHash(
        SafeChangeApplicationRequest request)
    {
        string? lineageId = GetLineageId(request);
        return lineageId is null
            ? null
            : Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes(lineageId)))
                .ToLowerInvariant();
    }

    private static bool IsValidLineageId(string? value) =>
        value is { Length: > 0 and <= MaximumLineageIdLength } &&
        value.IndexOfAny(['\0', '\r', '\n']) < 0;

    private static string LegacySuffix(
        SafeChangeApplicationRequest request) =>
        request.IsGovernedHumanCorrection
            ? "-governed-human-correction"
            : request.IsHumanReviewCorrection
                ? "-human-review-correction"
                : request.IsBuildCorrectionRetry
                    ? "-build-correction-retry"
                    : request.IsBuildCorrection
                        ? "-build-correction"
                        : string.Empty;
}

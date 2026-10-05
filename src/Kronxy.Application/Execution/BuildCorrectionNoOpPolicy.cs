using System.Security.Cryptography;
using System.Text;

namespace Kronxy.Application.Execution;

public static class BuildCorrectionNoOpPolicy
{
    public static IReadOnlyList<string> NoOpPaths(
        ValidatedDeveloperProposal proposal) =>
        proposal.Changes
            .Where(change =>
                change.Operation == DeveloperChangeOperationType.ReplaceFile &&
                string.Equals(
                    change.ExpectedContentSha256,
                    Convert.ToHexString(
                        SHA256.HashData(Encoding.UTF8.GetBytes(change.Content)))
                        .ToLowerInvariant(),
                    StringComparison.OrdinalIgnoreCase))
            .Select(change => change.RelativePath)
            .ToArray();

    public static bool IsEntireNoOp(
        ValidatedDeveloperProposal proposal) =>
        proposal.Changes.Count > 0 &&
        NoOpPaths(proposal).Count == proposal.Changes.Count;
}

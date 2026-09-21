using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;

namespace Kronxy.Infrastructure.Execution;

public sealed class ReviewDecisionPolicy : IReviewDecisionPolicy
{
    public ReviewDecisionResult Evaluate(ReviewDecisionInput input)
    {
        if (input is null || input.JobId == Guid.Empty || input.RunId == Guid.Empty ||
            input.Review is null || input.BuildReport is null || input.TestReport is null ||
            input.ObservedChanges is null ||
            input.BuildReport.JobId != input.JobId || input.BuildReport.RunId != input.RunId ||
            input.TestReport.JobId != input.JobId || input.TestReport.RunId != input.RunId ||
            input.ObservedChanges.JobId != input.JobId || input.ObservedChanges.RunId != input.RunId ||
            !ValidObserved(input.ObservedChanges))
            return ReviewDecisionResult.Failure("REVIEW_DECISION_EVIDENCE_INVALID");

        if (input.Review.Decision == ReviewerDecision.Rejected)
            return ReviewDecisionResult.Success(ReviewerDecision.Rejected);
        if (input.Review.Decision == ReviewerDecision.ChangesRequired)
            return ReviewDecisionResult.Success(ReviewerDecision.ChangesRequired);
        if (input.Review.Decision != ReviewerDecision.Approved ||
            !input.BuildReport.IsSuccess || !input.TestReport.IsSuccess)
            return ReviewDecisionResult.Failure("REVIEW_DECISION_GATES_FAILED");
        return ReviewDecisionResult.Success(ReviewerDecision.Approved);
    }

    private static bool ValidObserved(ObservedChangeManifest manifest)
    {
        if (manifest.Entries is null || string.IsNullOrWhiteSpace(manifest.BaseRepositoryHead) ||
            manifest.BaseRepositoryHead.Length is not (40 or 64))
            return false;
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (ObservedChangeManifestEntry entry in manifest.Entries)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.RelativePath) ||
                Path.IsPathRooted(entry.RelativePath) ||
                entry.RelativePath.Split('/').Any(segment => segment is "" or "." or "..") ||
                !paths.Add(entry.RelativePath))
                return false;
            bool deleted = entry.ChangeKind == ObservedRepositoryChangeKind.Deleted;
            if (deleted)
            {
                if (entry.FinalSha256 is not null || entry.FinalSizeBytes is not null) return false;
            }
            else if (entry.FinalSizeBytes is null || entry.FinalSizeBytes < 0 ||
                     entry.FinalSha256 is null || entry.FinalSha256.Length != 64 ||
                     !entry.FinalSha256.All(Uri.IsHexDigit))
                return false;
        }
        return true;
    }
}

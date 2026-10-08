using Kronxy.Application.Artifacts;
using Kronxy.Domain.Jobs;

namespace Kronxy.Application.Jobs;

public sealed class OperatorJobService : IOperatorJobService
{
    public const int MaximumPageSize = 100;
    private readonly IJobRepository jobs;
    private readonly IJobRunIdProvider runIds;
    private readonly IArtifactMetadataRepository artifacts;
    private readonly IArtifactReader reader;

    public OperatorJobService(IJobRepository jobs, IJobRunIdProvider runIds,
        IArtifactMetadataRepository artifacts, IArtifactReader reader)
    {
        this.jobs = jobs; this.runIds = runIds; this.artifacts = artifacts; this.reader = reader;
    }

    public async Task<OperatorJobPage> GetJobsAsync(int page, int pageSize,
        string? externalId, JobState? state, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        var result = await jobs.GetPageAsync(page, pageSize, externalId, state, cancellationToken);
        return new(result.Items.Select(ToSummary).ToArray(), page, pageSize, result.TotalItems,
            result.TotalItems == 0 ? 0 : (int)Math.Ceiling(result.TotalItems / (double)pageSize));
    }

    public Task<Job?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        jobs.GetByIdAsync(jobId, cancellationToken);

    public async Task<IReadOnlyList<OperatorHistoryItem>?> GetHistoryAsync(Guid jobId,
        CancellationToken cancellationToken = default)
    {
        Job? job = await jobs.GetByIdAsync(jobId, cancellationToken);
        if (job is null) return null;
        var result = new List<OperatorHistoryItem>();
        for (int i = 0; i < job.Transitions.Count; i++)
        {
            JobTransition item = job.Transitions[i];
            result.Add(new(i + 1, item.OccurredOnUtc, item.FromState.ToString(),
                item.ToState.ToString(), item.Actor, item.CorrelationId, item.Reason, null));
        }
        return result;
    }

    public async Task<IReadOnlyList<OperatorArtifactItem>?> GetArtifactsAsync(Guid jobId,
        CancellationToken cancellationToken = default)
    {
        Job? job = await jobs.GetByIdAsync(jobId, cancellationToken);
        if (job is null) return null;
        Guid runId = runIds.Create(job.Id, job.AttemptCount);
        IReadOnlyList<ArtifactRecord> records = await artifacts.GetByJobAndRunAsync(job.Id, runId, cancellationToken);
        return records.Select(a => new OperatorArtifactItem(a.ArtifactId, a.ArtifactType.ToString(),
            $"/api/jobs/{job.Id}/artifacts/{a.ArtifactId}", a.Sha256, a.CreatedAtUtc,
            a.CorrelationId, a.SizeBytes)).ToArray();
    }

    public async Task<OperatorArtifactContent?> GetArtifactAsync(Guid jobId, Guid artifactId,
        CancellationToken cancellationToken = default)
    {
        Job? job = await jobs.GetByIdAsync(jobId, cancellationToken);
        if (job is null) return null;
        Guid runId = runIds.Create(job.Id, job.AttemptCount);
        ArtifactReadResult result = await reader.ReadByIdAsync(artifactId, jobId, runId,
            cancellationToken: cancellationToken);
        if (!result.IsSuccess) return null;
        string type = IsJsonOrText(result.Content) ? "application/json" : "application/octet-stream";
        return new(result.Artifact!, result.Content, type, type == "application/json");
    }

    public async Task<OperatorLineage?> GetEffectiveLineageAsync(Guid jobId,
        CancellationToken cancellationToken = default)
    {
        Job? job = await jobs.GetByIdAsync(jobId, cancellationToken);
        if (job is null) return null;
        Guid runId = runIds.Create(job.Id, job.AttemptCount);
        IReadOnlyList<ArtifactRecord> a = await artifacts.GetByJobAndRunAsync(job.Id, runId, cancellationToken);
        string lineage = a.Any(x => x.ArtifactType == ArtifactType.GovernedHumanCorrectionReceipt)
            ? "GovernedHumanCorrection"
            : a.Any(x => x.ArtifactType is ArtifactType.DeveloperBuildCorrectionProposal or ArtifactType.DeveloperBuildCorrectionRetryProposal)
                ? "BuildCorrection" : "Developer";
        ArtifactRecord[] candidates = (lineage switch
        {
            "GovernedHumanCorrection" => a.Where(x => x.ArtifactType == ArtifactType.GovernedHumanCorrectionReceipt),
            "BuildCorrection" => a.Where(x => x.ArtifactType is ArtifactType.DeveloperBuildCorrectionProposal or ArtifactType.DeveloperBuildCorrectionRetryProposal),
            _ => a.Where(x => x.ArtifactType == ArtifactType.DeveloperProposal)
        }).OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.ArtifactId).ToArray();
        if (candidates.Length == 0 ||
            (candidates.Length > 1 && candidates[0].CreatedAtUtc == candidates[1].CreatedAtUtc))
            return null;
        ArtifactRecord source = candidates[0];
        bool observed = a.Any(x => x.ArtifactType is ArtifactType.ObservedChangeManifest or
            ArtifactType.ObservedBuildCorrectionManifest or ArtifactType.ObservedBuildCorrectionRetryManifest or
            ArtifactType.ObservedGovernedHumanCorrectionManifest);
        return new(lineage, source.CorrelationId, runId, job.AttemptCount, true, observed,
            Status(a, ArtifactType.BuildReport, ArtifactType.BuildCorrectionReport, ArtifactType.BuildCorrectionRetryReport, ArtifactType.BuildGovernedHumanCorrectionReport),
            Status(a, ArtifactType.TestReport, ArtifactType.TestGovernedHumanCorrectionReport),
            Status(a, ArtifactType.ReviewerReview, ArtifactType.ReviewerHumanReviewCorrectionReview));
    }

    public async Task<IReadOnlyList<OperatorAllowedAction>?> GetAllowedActionsAsync(Guid jobId,
        CancellationToken cancellationToken = default)
    {
        Job? job = await jobs.GetByIdAsync(jobId, cancellationToken);
        if (job is null) return null;
        string[] names = ["advance", "resume", "retryPending", "cancel", "humanReviewApprove",
            "humanReviewChangesRequired", "architectureDecision", "humanCorrection",
            "reviewerHumanReviewCorrectionSupersede"];
        return names.Select(name => new OperatorAllowedAction(name,
            JobWorkflow.IsOperatorActionAllowed(job, name),
            JobWorkflow.IsOperatorActionAllowed(job, name) ? null : "JOB_ACTION_NOT_ALLOWED_IN_CURRENT_STATE")).ToArray();
    }

    private OperatorJobSummary ToSummary(Job job) => new(job.Id, job.ExternalId, job.State.ToString(),
        job.AttemptCount, job.State == JobState.WaitingHuman, job.LastErrorCode, job.LastErrorMessage,
        job.CreatedOnUtc, job.UpdatedOnUtc, runIds.Create(job.Id, job.AttemptCount));
    private static string? Status(IReadOnlyList<ArtifactRecord> artifacts, params ArtifactType[] types) =>
        artifacts.Any(a => types.Contains(a.ArtifactType)) ? "Available" : null;
    private static bool IsJsonOrText(ReadOnlyMemory<byte> content)
    {
        if (content.IsEmpty) return true;
        foreach (byte value in content.Span)
        {
            if (char.IsWhiteSpace((char)value)) continue;
            return value is (byte)'{' or (byte)'[';
        }
        return true;
    }
}

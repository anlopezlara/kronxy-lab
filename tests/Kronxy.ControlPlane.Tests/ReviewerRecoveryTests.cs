using System.Text.Json;
using Kronxy.Application.AI;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Execution;
using Kronxy.Infrastructure.AI;
using Kronxy.Infrastructure.Artifacts;
using Kronxy.Infrastructure.Execution;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class ReviewerRecoveryTests
{
    private static readonly Guid JobId=Guid.NewGuid();
    private static readonly Guid RunId=Guid.NewGuid();

    [Fact]
    public async Task No_artifacts_is_not_completed()
    {
        var result=await Service(new Reader()).CheckAsync(Request());
        Assert.Equal(StageRecoveryStatus.NotCompleted,result.Status);
    }

    [Fact]
    public async Task Partial_evidence_is_invalid()
    {
        var reader=new Reader { Handler=r => r.ArtifactType==ArtifactType.ReviewerReview
            ? Success(r,JsonSerializer.SerializeToUtf8Bytes(Review()))
            : ArtifactReadResult.Failure(ArtifactReadFailureKind.NotFound,"NOT_FOUND") };
        Assert.Equal(StageRecoveryStatus.InvalidEvidence,(await Service(reader).CheckAsync(Request())).Status);
    }

    [Fact]
    public async Task Valid_pair_recovers_typed_review_without_ai()
    {
        var response=new AiResponse { Status=AiOperationStatus.Success,Content="{}",Provider="fake",
            LogicalModel="CodingQuality",PhysicalModel="fake",TerminationReason=AiTerminationReason.Stop };
        var reader=new Reader { Handler=r => r.ArtifactType switch
        {
            ArtifactType.ReviewerReview => Success(r,JsonSerializer.SerializeToUtf8Bytes(Review())),
            ArtifactType.ReviewerResponse => Success(r,JsonSerializer.SerializeToUtf8Bytes(response)),
            _ => ArtifactReadResult.Failure(ArtifactReadFailureKind.NotFound,"NOT_FOUND")
        } };
        var result=await Service(reader).CheckAsync(Request());
        Assert.True(result.IsCompleted);
        Assert.Equal(ReviewerDecision.Approved,result.ReviewerReview!.Decision);
    }

    [Fact]
    public async Task Effective_reviewer_prefers_superseding_pair_without_overwriting_history()
    {
        var response=new AiResponse { Status=AiOperationStatus.Success,Content="{}",Provider="fake",
            LogicalModel="CodingQuality",PhysicalModel="fake",TerminationReason=AiTerminationReason.Stop };
        ReviewerReview stale=Review() with { Decision=ReviewerDecision.ChangesRequired };
        var supersession=new ReviewerSupersessionEvidence
        {
            JobId=JobId,RunId=RunId,
            SupersededReviewArtifactType=ArtifactType.ReviewerHumanReviewCorrectionReview,
            EffectiveProposalLineage=DeveloperProposalLineage.HumanReviewCorrection,
            EffectiveProposalSha256=new string('a',64),
            ObservedChangesSha256=new string('b',64),
            BuildReportSha256=new string('c',64),
            TestReportSha256=new string('d',64),
            Decision=ReviewerDecision.Approved,
            RecordedAtUtc=DateTimeOffset.UtcNow
        };
        var reader=new Reader { Handler=r => r.ArtifactType switch
        {
            ArtifactType.ReviewerHumanReviewCorrectionSupersedingReview =>
                Success(r,JsonSerializer.SerializeToUtf8Bytes(Review())),
            ArtifactType.ReviewerHumanReviewCorrectionSupersedingResponse =>
                Success(r,JsonSerializer.SerializeToUtf8Bytes(response)),
            ArtifactType.ReviewerHumanReviewCorrectionSupersessionEvidence =>
                Success(r,JsonSerializer.SerializeToUtf8Bytes(supersession)),
            ArtifactType.ReviewerHumanReviewCorrectionReview =>
                Success(r,JsonSerializer.SerializeToUtf8Bytes(stale)),
            ArtifactType.ReviewerHumanReviewCorrectionResponse =>
                Success(r,JsonSerializer.SerializeToUtf8Bytes(response)),
            _ => ArtifactReadResult.Failure(ArtifactReadFailureKind.NotFound,"NOT_FOUND")
        } };

        StageRecoveryResult result=await Service(reader).CheckAsync(Request());

        Assert.True(result.IsCompleted);
        Assert.Equal(ReviewerDecision.Approved,result.ReviewerReview!.Decision);
    }

    private static ReviewerReview Review()=>new()
    { Decision=ReviewerDecision.Approved,Findings=[],RequiredCorrections=[],RiskAssessment="low",Summary="ok" };
    private static StageRecoveryRequest Request()=>new()
    { JobId=JobId,RunId=RunId,Stage=RecoveryStage.Reviewer };
    private static StageRecoveryEvidenceService Service(IArtifactReader reader)=>new(reader,
        new ArtifactStoreOptions { RootPath=Path.GetTempPath(),MaxArtifactBytes=1024*1024 });
    private static ArtifactReadResult Success(ArtifactReadRequest r,byte[] bytes)=>ArtifactReadResult.Success(new ArtifactRecord
    { ArtifactId=Guid.NewGuid(),JobId=r.JobId,RunId=r.RunId,ArtifactType=r.ArtifactType,
      RelativePath="review/evidence.json",Sha256=new string('a',64),SizeBytes=bytes.Length,
      CreatedAtUtc=DateTimeOffset.UtcNow,CorrelationId=r.CorrelationId },bytes);
    private sealed class Reader:IArtifactReader
    {
        public Func<ArtifactReadRequest,ArtifactReadResult>? Handler { get; init; }
        public Task<ArtifactReadResult> ReadAsync(ArtifactReadRequest r,CancellationToken cancellationToken=default)=>
            Task.FromResult(Handler?.Invoke(r)??ArtifactReadResult.Failure(ArtifactReadFailureKind.NotFound,"NOT_FOUND"));
    }
}

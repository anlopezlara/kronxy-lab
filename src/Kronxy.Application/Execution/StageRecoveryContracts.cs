using Kronxy.Application.Artifacts;

namespace Kronxy.Application.Execution;

public enum RecoveryStage
{
    Context = 10,
    DevelopmentAnalysis = 15,
    ArchitectureDecision = 16,
    Planning = 20,
    Developer = 25,
    DeveloperOriginal = 26,
    DeveloperBuildCorrection = 27,
    DeveloperBuildCorrectionRetry = 34,
    HumanReviewCorrection = 28,
    DeveloperHumanReviewCorrection = 29,
    ObservedChanges = 30,
    ObservedBuildCorrection = 31,
    ObservedBuildCorrectionRetry = 36,
    ObservedHumanReviewCorrection = 32,
    EffectiveDeveloperProposal = 33,
    Restore = 35,
    Build = 40,
    BuildHumanReviewCorrection = 41,
    Test = 50,
    TestHumanReviewCorrection = 51,
    ReviewerOriginal = 59,
    Reviewer = 60,
    ReviewerHumanReviewCorrection = 61,
    ReviewerHumanReviewCorrectionSuperseding = 62,
    ReviewerHumanReviewCorrectionSourceAwareSuperseding = 63,
    HumanReviewApproval = 64,
    GovernedHumanCorrection = 65,
    ObservedGovernedHumanCorrection = 66,
    BuildGovernedHumanCorrection = 67,
    TestGovernedHumanCorrection = 68
}

public enum DeveloperProposalLineage
{
    Original = 10,
    BuildCorrection = 20,
    HumanReviewCorrection = 30,
    GovernedHumanCorrection = 40
}

public enum StageRecoveryStatus
{
    NotCompleted = 0,
    Completed = 10,
    FailedExecution = 15,
    InvalidEvidence = 20,
    Cancelled = 30,
    Failure = 40
}

public sealed record StageRecoveryRequest
{
    public required Guid JobId { get; init; }

    public required Guid RunId { get; init; }

    public required RecoveryStage Stage { get; init; }

    public string JobRequest { get; init; } =
        string.Empty;

    public string CorrelationId { get; init; } =
        string.Empty;

    public int AttemptCount { get; init; }
}

public sealed record StageRecoveryResult(
    StageRecoveryStatus Status,
    string ErrorCode,
    ValidatedDeveloperProposal? DeveloperProposal = null,
    BuildExecutionReport? BuildReport = null,
    TestExecutionReport? TestReport = null,
    ObservedChangeManifest? ObservedChangeManifest = null,
    PlannerPlan? PlannerPlan = null,
    ReviewerReview? ReviewerReview = null,
    string? BuildStandardOutput = null,
    HumanReviewCorrectionEvidence? HumanReviewCorrection = null,
    DeveloperProposalLineage? DeveloperProposalLineage = null,
    GovernedHumanCorrectionEvidence? GovernedHumanCorrection = null,
    GovernedHumanCorrectionReceipt? GovernedHumanCorrectionReceipt = null,
    DevelopmentAnalysis? DevelopmentAnalysis = null,
    ArtifactRecord? DevelopmentAnalysisArtifact = null,
    ArchitectureDecisionEvidence? ArchitectureDecision = null)
{
    public bool IsCompleted =>
        Status == StageRecoveryStatus.Completed;

    public static StageRecoveryResult Completed(
        ValidatedDeveloperProposal? developerProposal = null,
        BuildExecutionReport? buildReport = null,
        TestExecutionReport? testReport = null,
        ObservedChangeManifest? observedChangeManifest = null,
        PlannerPlan? plannerPlan = null,
        ReviewerReview? reviewerReview = null,
        string? buildStandardOutput = null,
        HumanReviewCorrectionEvidence? humanReviewCorrection = null,
        DeveloperProposalLineage? developerProposalLineage = null,
        GovernedHumanCorrectionEvidence? governedHumanCorrection = null,
        GovernedHumanCorrectionReceipt? governedHumanCorrectionReceipt = null,
        DevelopmentAnalysis? developmentAnalysis = null,
        ArtifactRecord? developmentAnalysisArtifact = null,
        ArchitectureDecisionEvidence? architectureDecision = null) =>
        new(
            StageRecoveryStatus.Completed,
            string.Empty,
            developerProposal,
            buildReport,
            testReport,
            observedChangeManifest,
            plannerPlan,
            reviewerReview,
            buildStandardOutput,
            humanReviewCorrection,
            developerProposalLineage,
            governedHumanCorrection,
            governedHumanCorrectionReceipt,
            developmentAnalysis,
            developmentAnalysisArtifact,
            architectureDecision);

    public static StageRecoveryResult FailedBuild(
        BuildExecutionReport report,
        string standardOutput) =>
        new(
            StageRecoveryStatus.FailedExecution,
            string.Empty,
            BuildReport: report,
            BuildStandardOutput: standardOutput);

    public static StageRecoveryResult NotCompleted() =>
        new(
            StageRecoveryStatus.NotCompleted,
            string.Empty);

    public static StageRecoveryResult InvalidEvidence(
        string errorCode) =>
        new(
            StageRecoveryStatus.InvalidEvidence,
            errorCode);

    public static StageRecoveryResult Cancelled(
        string errorCode) =>
        new(
            StageRecoveryStatus.Cancelled,
            errorCode);

    public static StageRecoveryResult Failure(
        string errorCode) =>
        new(
            StageRecoveryStatus.Failure,
            errorCode);
}

public interface IStageRecoveryEvidenceService
{
    Task<StageRecoveryResult> CheckAsync(
        StageRecoveryRequest request,
        CancellationToken cancellationToken = default);
}

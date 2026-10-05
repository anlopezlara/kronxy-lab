using Kronxy.Domain.Abstractions;

namespace Kronxy.Application.Jobs;

public static class JobApplicationErrors
{
	public static readonly Error NotFound = new Error("Job.NotFound", "The job was not found.");

	public static readonly Error InvalidConfiguration = new Error("Job.InvalidConfiguration", "The control plane configuration is invalid.");


	public static readonly Error InvalidExternalId =
	        new Error(
	                "Job.InvalidExternalId",
	                "The external job identifier must use the KRONXY canonical format.");
	public static readonly Error TerminalJob = new Error("Job.TerminalJob", "The requested operation is not valid for a terminal job.");

	public static readonly Error NoNextState = new Error("Job.NoNextState", "The job does not have an automatic next state.");

	public static readonly Error TimedOut = new Error("Job.TimedOut", "The maximum job duration was exceeded.");

        public static readonly Error SourceRevisionUnavailable =
                new Error(
                        "Job.SourceRevisionUnavailable",
                        "The authoritative repository revision could not be resolved.");

        public static readonly Error MissingBaseRepositoryHead =
                new Error(
                        "Job.MissingBaseRepositoryHead",
                        "The job does not have a persisted base repository revision.");

        public static readonly Error ExecutionPlanePreparationFailed =
                new Error(
                        "Job.ExecutionPlanePreparationFailed",
                        "The execution workspace could not be prepared.");

        public static readonly Error ExecutionPlaneRepositoryMismatch =
                new Error(
                        "Job.ExecutionPlaneRepositoryMismatch",
                        "The execution workspace does not match the pinned repository revision.");

        public static readonly Error ContextGenerationFailed =
                new Error(
                        "Job.ContextGenerationFailed",
                        "The context package could not be generated.");

        public static readonly Error DevelopmentAnalysisFailed =
                new Error(
                        "Job.DevelopmentAnalysisFailed",
                        "Development analysis could not be completed or persisted.");

        public static readonly Error PlanningExecutionFailed =
                new Error(
                        "Job.PlanningExecutionFailed",
                        "The AI planning execution did not complete successfully.");

        public static readonly Error DeveloperExecutionFailed =
                new Error(
                        "Job.DeveloperExecutionFailed",
                        "The AI developer execution did not complete successfully.");

        public static readonly Error SafeChangeApplicationFailed =
                new Error(
                        "Job.SafeChangeApplicationFailed",
                        "The validated developer proposal could not be safely applied.");

        public static readonly Error RestoreExecutionFailed =
                new Error(
                        "Job.RestoreExecutionFailed",
                        "The restore execution did not complete successfully.");

        public static readonly Error BuildExecutionFailed =
                new Error(
                        "Job.BuildExecutionFailed",
                        "The build execution did not complete successfully.");

        public static readonly Error TestExecutionFailed =
                new Error(
                        "Job.TestExecutionFailed",
                        "The test execution did not complete successfully.");

        public static readonly Error ObservedChangeEvidenceInvalid =
                new Error(
                        "Job.ObservedChangeEvidenceInvalid",
                        "Observed change evidence could not be generated or validated.");

        public static readonly Error ReviewerEvidenceInvalid = new("Job.ReviewerEvidenceInvalid", "Required persisted reviewer evidence is incomplete or invalid.");
        public static readonly Error ReviewerExecutionFailed = new("Job.ReviewerExecutionFailed", "The reviewer execution failed closed.");
        public static readonly Error ReviewDecisionFailed = new("Job.ReviewDecisionFailed", "Deterministic review gates rejected the evidence.");

    public static readonly Error StageRecoveryFailed =
        new(
            "Jobs.StageRecoveryFailed",
            "Persisted stage evidence could not be safely recovered.");

}

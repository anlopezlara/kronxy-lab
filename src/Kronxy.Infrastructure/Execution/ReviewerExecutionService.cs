using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kronxy.Application.AI;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Execution;
using Kronxy.Infrastructure.AI;
using Kronxy.Infrastructure.Artifacts;

namespace Kronxy.Infrastructure.Execution;

public sealed class ReviewerExecutionService : IReviewerExecutionService
{
    private const string Instructions =
        "You are the KRONXY reviewer. Assess only the persisted evidence supplied. " +
        "Do not execute commands, access files, call tools, or request operational capabilities. " +
        "Return the closed structured review. KRONXY alone makes the final decision.";

    private const string HumanCorrectionInstructions =
        " This review follows a Human Review ChangesRequired decision. " +
        "Verify every supplied human correction explicitly against the effective proposal and observed evidence. " +
        "Do not replace itemized verification with a generic pattern-compliance statement.";

    private readonly IAiGateway aiGateway;
    private readonly IArtifactStore artifactStore;
    private readonly IDeterministicAcceptanceGate deterministicAcceptanceGate;
    private readonly AiGatewayOptions aiOptions;
    private readonly AiStructuredOutputValidator validator = new();

    public ReviewerExecutionService(
        IAiGateway aiGateway,
        IArtifactStore artifactStore,
        AiGatewayOptions aiOptions,
        IDeterministicAcceptanceGate? deterministicAcceptanceGate = null)
    {
        this.aiGateway = aiGateway ?? throw new ArgumentNullException(nameof(aiGateway));
        this.artifactStore = artifactStore ?? throw new ArgumentNullException(nameof(artifactStore));
        this.deterministicAcceptanceGate = deterministicAcceptanceGate ??
            new DeterministicAcceptanceGate();
        this.aiOptions = aiOptions ?? throw new ArgumentNullException(nameof(aiOptions));
        this.aiOptions.Validate();
    }

    public async Task<ReviewerExecutionResult> ExecuteAsync(ReviewerExecutionRequest request, CancellationToken cancellationToken = default)
    {
        if (!ValidRequest(request))
            return Failure(ReviewerExecutionFailureKind.InvalidRequest, "REVIEWER_INVALID_REQUEST");

        try
        {
            DeterministicAcceptanceGateResult deterministicGate =
                deterministicAcceptanceGate.Evaluate(
                    request.JobRequest, request.Plan, request.EffectiveSourceSnapshot);
            string content = JsonSerializer.Serialize(new
            {
                originalRequest = request.JobRequest,
                plannerPlan = request.Plan,
                acceptanceCriteria = request.Plan.AcceptanceCriteria,
                requiredVerifications = request.HumanReviewCorrection?
                    .RequiredCorrections.Select(correction => new
                    {
                        correction.RelativePath,
                        correction.Instruction
                    }),
                effectiveProposalKind =
                    request.EffectiveProposalLineage.ToString(),
                effectiveProposalArtifact =
                    EffectiveProposalArtifact(request.EffectiveProposalLineage),
                correctionLineage =
                    request.EffectiveProposalLineage.ToString(),
                effectiveDeveloperProposal = request.DeveloperProposal,
                effectiveFinalSource = request.EffectiveSourceSnapshot,
                deterministicAcceptanceGate = deterministicGate,
                semanticAcceptanceCriteria = deterministicGate.SemanticCriteria,
                observedChangeManifest = request.ObservedChanges,
                buildReport = request.BuildReport,
                testReport = request.TestReport,
                humanReviewCorrection = request.HumanReviewCorrection
            });
            if ((long)Instructions.Length + content.Length > aiOptions.MaxInputCharacters)
                return Failure(ReviewerExecutionFailureKind.InvalidRequest, "REVIEWER_INPUT_LIMIT_EXCEEDED");

            AiResponse response = await aiGateway.GenerateAsync(new AiRequest
            {
                Model = AiLogicalModel.CodingQuality,
                SystemInstructions = Instructions +
                    (request.HumanReviewCorrection is null
                        ? string.Empty
                        : HumanCorrectionInstructions),
                UserContent = content,
                CorrelationId = request.CorrelationId,
                Generation = new AiGenerationOptions { MaxOutputTokens = aiOptions.MaxOutputTokens, Temperature = 0 },
                StructuredOutput = new AiStructuredOutput { Schema = ReviewerContractSchema.CreateSchema() }
            }, cancellationToken);

            if (!response.IsSuccess)
                return Failure(ReviewerExecutionFailureKind.AiFailure,
                    string.IsNullOrWhiteSpace(response.ErrorCode) ? "REVIEWER_AI_FAILED" : response.ErrorCode);
            if (!validator.TryValidate(ReviewerContractSchema.CreateSchema(), response.Content, out _))
                return Failure(ReviewerExecutionFailureKind.AiInvalidResponse, "REVIEWER_SCHEMA_INVALID");

            ReviewerReview? review;
            try { review = JsonSerializer.Deserialize<ReviewerReview>(response.Content); }
            catch (JsonException) { return Failure(ReviewerExecutionFailureKind.AiInvalidResponse, "REVIEWER_JSON_INVALID"); }
            if (!ValidReview(review))
                return Failure(ReviewerExecutionFailureKind.AiInvalidResponse, "REVIEWER_REVIEW_INVALID");

            review = ReconcileDeterministicEvidence(review!, deterministicGate);

            ArtifactWriteResult reviewWrite = await Write(
                request,
                ReviewArtifactType(request),
                JsonSerializer.SerializeToUtf8Bytes(review), cancellationToken);
            if (!reviewWrite.IsSuccess || reviewWrite.Artifact is null)
                return Failure(ReviewerExecutionFailureKind.ArtifactWriteFailure, "REVIEWER_REVIEW_WRITE_FAILED");

            ArtifactWriteResult responseWrite = await Write(
                request,
                ResponseArtifactType(request),
                JsonSerializer.SerializeToUtf8Bytes(response), cancellationToken);
            if (!responseWrite.IsSuccess || responseWrite.Artifact is null)
                return Failure(ReviewerExecutionFailureKind.ArtifactWriteFailure, "REVIEWER_RESPONSE_WRITE_FAILED");

            if (request.IsSupersedingHumanReviewCorrection)
            {
                var evidence = new ReviewerSupersessionEvidence
                {
                    JobId = request.JobId,
                    RunId = request.RunId,
                    SupersededReviewArtifactType =
                        ArtifactType.ReviewerHumanReviewCorrectionReview,
                    EffectiveProposalLineage =
                        request.EffectiveProposalLineage,
                    EffectiveProposalSha256 = Hash(request.DeveloperProposal),
                    ObservedChangesSha256 = Hash(request.ObservedChanges),
                    BuildReportSha256 = Hash(request.BuildReport),
                    TestReportSha256 = Hash(request.TestReport),
                    ReviewVersion = request.SupersedingReviewVersion,
                    EffectiveSourceSnapshotSha256 =
                        Hash(request.EffectiveSourceSnapshot),
                    DeterministicAcceptanceGateSha256 = Hash(deterministicGate),
                    ReviewerContradictsDeterministicEvidence =
                        review!.ReviewerContradictsDeterministicEvidence,
                    Decision = review!.Decision,
                    RecordedAtUtc = DateTimeOffset.UtcNow
                };
                ArtifactWriteResult evidenceWrite = await Write(
                    request,
                    request.SupersedingReviewVersion switch
                    {
                        3 => ArtifactType.ReviewerHumanReviewCorrectionDeterministicSupersessionEvidence,
                        2 => ArtifactType.ReviewerHumanReviewCorrectionSourceAwareSupersessionEvidence,
                        _ => ArtifactType.ReviewerHumanReviewCorrectionSupersessionEvidence
                    },
                    JsonSerializer.SerializeToUtf8Bytes(evidence),
                    cancellationToken);
                if (!evidenceWrite.IsSuccess)
                    return Failure(
                        ReviewerExecutionFailureKind.ArtifactWriteFailure,
                        "REVIEWER_SUPERSESSION_EVIDENCE_WRITE_FAILED");
            }

            return ReviewerExecutionResult.Success(review!, reviewWrite.Artifact, responseWrite.Artifact);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { return Failure(ReviewerExecutionFailureKind.Cancelled, "REVIEWER_CANCELLED"); }
        catch { return Failure(ReviewerExecutionFailureKind.InternalFailure, "REVIEWER_FAILED"); }
    }

    internal static bool ValidReview(ReviewerReview? value) =>
        value is not null && Enum.IsDefined(value.Decision) &&
        value.Findings is not null && value.RequiredCorrections is not null &&
        value.Findings.Count <= 100 && value.RequiredCorrections.Count <= 100 &&
        !string.IsNullOrWhiteSpace(value.RiskAssessment) && value.RiskAssessment.Length <= 4000 &&
        !string.IsNullOrWhiteSpace(value.Summary) && value.Summary.Length <= 4000 &&
        value.Findings.All(x => x is not null && !string.IsNullOrWhiteSpace(x.Category) &&
            x.Category.Length <= 200 && !string.IsNullOrWhiteSpace(x.Description) && x.Description.Length <= 4000) &&
        value.RequiredCorrections.All(x => x is not null && !string.IsNullOrWhiteSpace(x.RelativePath) &&
            x.RelativePath.Length <= 512 && !string.IsNullOrWhiteSpace(x.Instruction) && x.Instruction.Length <= 4000);

    internal static ReviewerReview ReconcileDeterministicEvidence(
        ReviewerReview review,
        DeterministicAcceptanceGateResult gate)
    {
        DeterministicAcceptanceCriterionResult[] passed = gate.Criteria
            .Where(result => result.Status == DeterministicCriterionStatus.Pass)
            .ToArray();
        bool Contradicts(string text) => passed.Any(result =>
            text.Contains(result.Criterion.Expected, StringComparison.OrdinalIgnoreCase) &&
            NegativeAbsenceClaim(text));

        ReviewerFinding[] findings = review.Findings
            .Where(finding => !Contradicts(finding.Description)).ToArray();
        ReviewerCorrection[] corrections = review.RequiredCorrections
            .Where(correction => !Contradicts(correction.Instruction)).ToArray();
        bool contradicted = findings.Length != review.Findings.Count ||
            corrections.Length != review.RequiredCorrections.Count;

        DeterministicAcceptanceCriterionResult[] failed = gate.Criteria
            .Where(result => result.Status != DeterministicCriterionStatus.Pass).ToArray();
        if (failed.Length > 0)
        {
            findings = findings.Concat(failed.Select(result => new ReviewerFinding
            {
                Category = "DeterministicAcceptance",
                Description = $"{result.Criterion.Id}: {result.Evidence}"
            })).ToArray();
            corrections = corrections.Concat(failed.Select(result => new ReviewerCorrection
            {
                RelativePath = result.Criterion.RelativePath,
                Instruction = $"Satisfy deterministic criterion {result.Criterion.Id}."
            })).ToArray();
        }

        ReviewerDecision decision = failed.Length > 0
            ? ReviewerDecision.ChangesRequired
            : review.Decision == ReviewerDecision.ChangesRequired &&
              findings.Length == 0 && corrections.Length == 0 && contradicted
                ? ReviewerDecision.Approved
                : review.Decision;
        return review with
        {
            Decision = decision,
            Findings = findings,
            RequiredCorrections = corrections,
            DeterministicAcceptanceGate = gate,
            ReviewerContradictsDeterministicEvidence = contradicted
        };
    }

    private static bool NegativeAbsenceClaim(string text) =>
        new[] { "missing", "does not exist", "doesn't exist", "not present", "not found", "lacks", "is absent" }
            .Any(phrase => text.Contains(phrase, StringComparison.OrdinalIgnoreCase));

    private static bool ValidRequest(ReviewerExecutionRequest r) =>
        r is not null && r.JobId != Guid.Empty && r.RunId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(r.JobRequest) && r.Plan is not null &&
        r.DeveloperProposal is not null && r.ObservedChanges is not null &&
        r.BuildReport is not null && r.TestReport is not null &&
        r.EffectiveSourceSnapshot is not null &&
        r.ObservedChanges.JobId == r.JobId && r.ObservedChanges.RunId == r.RunId &&
        r.BuildReport.JobId == r.JobId && r.BuildReport.RunId == r.RunId &&
        r.TestReport.JobId == r.JobId && r.TestReport.RunId == r.RunId &&
        r.EffectiveSourceSnapshot.JobId == r.JobId &&
        r.EffectiveSourceSnapshot.RunId == r.RunId &&
        r.EffectiveSourceSnapshot.EffectiveProposalLineage ==
            r.EffectiveProposalLineage &&
        HasExactEffectiveSourceScope(r.Plan, r.EffectiveSourceSnapshot) &&
        Enum.IsDefined(r.EffectiveProposalLineage) &&
        (r.EffectiveProposalLineage !=
            DeveloperProposalLineage.HumanReviewCorrection ||
            (r.HumanReviewCorrection is not null &&
             HasExactHumanCorrectionPaths(
                r.DeveloperProposal,
                r.HumanReviewCorrection))) &&
        (r.HumanReviewCorrection is null ||
            r.EffectiveProposalLineage ==
                DeveloperProposalLineage.HumanReviewCorrection) &&
        (!r.IsSupersedingHumanReviewCorrection ||
            r.EffectiveProposalLineage ==
                DeveloperProposalLineage.HumanReviewCorrection) &&
        (!r.IsSupersedingHumanReviewCorrection ||
            r.SupersedingReviewVersion is 1 or 2 or 3) &&
        (r.HumanReviewCorrection is null ||
            (r.HumanReviewCorrection.JobId == r.JobId &&
             r.HumanReviewCorrection.RunId == r.RunId &&
             r.HumanReviewCorrection.Decision == HumanReviewDecision.ChangesRequired &&
             r.HumanReviewCorrection.RequiredCorrections.Count > 0));

    private static bool HasExactEffectiveSourceScope(
        PlannerPlan plan,
        ReviewerEffectiveSourceSnapshot snapshot)
    {
        if (snapshot.Files is null || snapshot.Files.Count == 0 ||
            snapshot.Files.Any(file =>
                string.IsNullOrWhiteSpace(file.RelativePath) ||
                string.IsNullOrWhiteSpace(file.Sha256) ||
                file.Sha256.Length != 64 ||
                file.SizeBytes < 0 || file.Content is null ||
                file.SizeBytes != Encoding.UTF8.GetByteCount(file.Content) ||
                !string.Equals(
                    file.Sha256,
                    Convert.ToHexString(SHA256.HashData(
                        Encoding.UTF8.GetBytes(file.Content))).ToLowerInvariant(),
                    StringComparison.OrdinalIgnoreCase)))
            return false;

        HashSet<string> planned = plan.CandidateFilesToModify
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> captured = snapshot.Files
            .Select(file => file.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return planned.Count == plan.CandidateFilesToModify.Count &&
            captured.Count == snapshot.Files.Count &&
            planned.SetEquals(captured);
    }

    private static bool HasExactHumanCorrectionPaths(
        ValidatedDeveloperProposal proposal,
        HumanReviewCorrectionEvidence evidence)
    {
        HashSet<string> proposed = proposal.Changes
            .Select(change => change.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> required = evidence.RequiredCorrections
            .Select(correction => correction.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return proposed.Count > 0 && proposed.SetEquals(required);
    }

    private static ArtifactType ReviewArtifactType(
        ReviewerExecutionRequest request) =>
        request.IsSupersedingHumanReviewCorrection
            ? request.SupersedingReviewVersion switch
            {
                3 => ArtifactType.ReviewerHumanReviewCorrectionDeterministicSupersedingReview,
                2 => ArtifactType.ReviewerHumanReviewCorrectionSourceAwareSupersedingReview,
                _ => ArtifactType.ReviewerHumanReviewCorrectionSupersedingReview
            }
            : request.HumanReviewCorrection is null
                ? ArtifactType.ReviewerReview
                : ArtifactType.ReviewerHumanReviewCorrectionReview;

    private static string EffectiveProposalArtifact(
        DeveloperProposalLineage lineage) =>
        lineage switch
        {
            DeveloperProposalLineage.Original =>
                ArtifactType.DeveloperProposal.ToString(),
            DeveloperProposalLineage.BuildCorrection =>
                ArtifactType.DeveloperBuildCorrectionProposal.ToString(),
            DeveloperProposalLineage.HumanReviewCorrection =>
                ArtifactType.DeveloperHumanReviewCorrectionProposal.ToString(),
            DeveloperProposalLineage.GovernedHumanCorrection =>
                ArtifactType.GovernedHumanCorrectionRequest.ToString(),
            _ => throw new ArgumentOutOfRangeException(nameof(lineage))
        };

    private static ArtifactType ResponseArtifactType(
        ReviewerExecutionRequest request) =>
        request.IsSupersedingHumanReviewCorrection
            ? request.SupersedingReviewVersion switch
            {
                3 => ArtifactType.ReviewerHumanReviewCorrectionDeterministicSupersedingResponse,
                2 => ArtifactType.ReviewerHumanReviewCorrectionSourceAwareSupersedingResponse,
                _ => ArtifactType.ReviewerHumanReviewCorrectionSupersedingResponse
            }
            : request.HumanReviewCorrection is null
                ? ArtifactType.ReviewerResponse
                : ArtifactType.ReviewerHumanReviewCorrectionResponse;

    private Task<ArtifactWriteResult> Write(ReviewerExecutionRequest r, ArtifactType type, byte[] bytes, CancellationToken ct) =>
        artifactStore.WriteAsync(new ArtifactWriteRequest { JobId=r.JobId, RunId=r.RunId, ArtifactType=type,
            Content=bytes, CorrelationId=r.CorrelationId }, ct);
    private static string Hash<T>(T value) =>
        Convert.ToHexString(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();
    private static ReviewerExecutionResult Failure(ReviewerExecutionFailureKind kind,string code) =>
        ReviewerExecutionResult.Failure(kind, code);
}

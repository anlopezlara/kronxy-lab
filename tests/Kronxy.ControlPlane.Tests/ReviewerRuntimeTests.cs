using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kronxy.Application.AI;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;
using Kronxy.Infrastructure.AI;
using Kronxy.Infrastructure.Execution;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class ReviewerRuntimeTests
{
    private static readonly Guid JobId = Guid.NewGuid();
    private static readonly Guid RunId = Guid.NewGuid();

    [Theory]
    [InlineData("Approved")]
    [InlineData("ChangesRequired")]
    [InlineData("Rejected")]
    public void Contract_accepts_only_closed_decisions(string decision)
    {
        string json = ReviewJson(decision);
        Assert.True(new AiStructuredOutputValidator().TryValidate(ReviewerContractSchema.CreateSchema(), json, out _));
        Assert.True(ReviewerExecutionService.ValidReview(JsonSerializer.Deserialize<ReviewerReview>(json)));
    }

    [Theory]
    [InlineData("Pass")]
    [InlineData("LGTM")]
    [InlineData("MostlyApproved")]
    [InlineData("PartialApproval")]
    public void Contract_rejects_unknown_decisions(string decision) =>
        Assert.False(new AiStructuredOutputValidator().TryValidate(
            ReviewerContractSchema.CreateSchema(), ReviewJson(decision), out _));

    [Fact]
    public async Task Runtime_persists_review_before_response_and_exposes_no_operational_input()
    {
        var ai = new FakeAi { Response = Success(ReviewJson("Approved")) };
        var store = new OrderedStore();
        var result = await new ReviewerExecutionService(ai, store, Options()).ExecuteAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.Equal([ArtifactType.ReviewerReview, ArtifactType.ReviewerResponse], store.Types);
        Assert.NotNull(ai.Request!.StructuredOutput);
        Assert.DoesNotContain("RepositoryManager", ai.Request.UserContent);
        Assert.DoesNotContain("WorkspaceManager", ai.Request.UserContent);
        Assert.Contains("effectiveFinalSource", ai.Request.UserContent);
        Assert.Contains("final source", ai.Request.UserContent);
    }

    [Fact]
    public async Task Invalid_structured_output_is_not_persisted()
    {
        var ai = new FakeAi { Response = Success("{\"decision\":\"Pass\"}") };
        var store = new OrderedStore();
        var result = await new ReviewerExecutionService(ai, store, Options()).ExecuteAsync(Request());
        Assert.Equal(ReviewerExecutionFailureKind.AiInvalidResponse, result.FailureKind);
        Assert.Empty(store.Types);
    }

    [Fact]
    public async Task Superseding_human_review_uses_separate_artifacts_and_itemized_criteria()
    {
        string[] codes =
        [
            "ProjectModule.NotFound",
            "ProjectModule.WrongParent",
            "ProjectModule.ParentNotFound",
            "ProjectModule.ParentInactive",
            "ProjectModule.HasActiveChildren"
        ];
        ReviewerExecutionRequest baseline = Request();
        string modelPath = "src/Kronxy.Domain/ProjectModules/ProjectModule.cs";
        string errorsPath = "src/Kronxy.Domain/ProjectModules/ProjectModuleErrors.cs";
        string repositoryPath = "src/Kronxy.Domain/ProjectModules/IProjectModuleRepository.cs";
        string modelContent =
            "Id ProjectId Name Description IsActive CreatedOnUtc UpdatedOnUtc DeletedOnUtc " +
            "Create Update Activate Deactivate";
        string errorsContent = string.Join(' ', codes);
        string repositoryContent = "GetByIdAsync Add";
        ReviewerExecutionRequest request = baseline with
        {
            EffectiveProposalLineage =
                DeveloperProposalLineage.HumanReviewCorrection,
            IsSupersedingHumanReviewCorrection = true,
            SupersedingReviewVersion = 2,
            DeveloperProposal = new ValidatedDeveloperProposal(
                "Human correction",
                [
                    new ValidatedDeveloperChange(
                        DeveloperChangeOperationType.ReplaceFile,
                        errorsPath,
                        "Complete the error catalog.",
                        "corrected content",
                        new string('a', 64),
                        17)
                ],
                [], [], 17, 100),
            Plan = baseline.Plan with
            {
                CandidateFilesToModify = [modelPath, errorsPath, repositoryPath],
                AcceptanceCriteria = codes
            },
            EffectiveSourceSnapshot = new ReviewerEffectiveSourceSnapshot(
                JobId,
                RunId,
                DeveloperProposalLineage.HumanReviewCorrection,
                [
                    Source(modelPath, modelContent),
                    Source(errorsPath, errorsContent),
                    Source(repositoryPath, repositoryContent)
                ]),
            HumanReviewCorrection = new HumanReviewCorrectionEvidence
            {
                JobId = JobId,
                RunId = RunId,
                Decision = HumanReviewDecision.ChangesRequired,
                RequiredCorrections = codes.Skip(1).Select(code =>
                    new HumanReviewRequiredCorrection
                    {
                        RelativePath = errorsPath,
                        Instruction = $"Require {code}."
                    }).ToArray(),
                RecordedAtUtc = DateTimeOffset.UtcNow
            }
        };
        var ai = new FakeAi { Response = Success(ReviewJson("Approved")) };
        var store = new OrderedStore();

        ReviewerExecutionResult result = await new ReviewerExecutionService(
            ai, store, Options()).ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [
                ArtifactType.ReviewerHumanReviewCorrectionSourceAwareSupersedingReview,
                ArtifactType.ReviewerHumanReviewCorrectionSourceAwareSupersedingResponse,
                ArtifactType.ReviewerHumanReviewCorrectionSourceAwareSupersessionEvidence
            ],
            store.Types);
        string prompt = ai.Request!.UserContent;
        Assert.All(codes, code => Assert.Contains(code, prompt));
        Assert.Contains(modelPath, prompt);
        Assert.Contains(errorsPath, prompt);
        Assert.Contains(repositoryPath, prompt);
        foreach (string criterion in new[]
        {
            "Id", "ProjectId", "Name", "Description", "IsActive",
            "CreatedOnUtc", "UpdatedOnUtc", "DeletedOnUtc",
            "Create", "Update", "Activate", "Deactivate",
            "GetByIdAsync", "Add"
        })
            Assert.Contains(criterion, prompt);
        Assert.Contains("effectiveDeveloperProposal", prompt);
        Assert.Contains("effectiveFinalSource", prompt);
        Assert.Contains("observedChangeManifest", prompt);
        Assert.Contains("buildReport", prompt);
        Assert.Contains("testReport", prompt);
        Assert.Contains("HumanReviewCorrection", prompt);
    }

    [Fact]
    public async Task Human_correction_rejects_stale_proposal_paths_before_ai()
    {
        ReviewerExecutionRequest baseline = Request();
        ReviewerExecutionRequest request = baseline with
        {
            EffectiveProposalLineage =
                DeveloperProposalLineage.HumanReviewCorrection,
            EffectiveSourceSnapshot = baseline.EffectiveSourceSnapshot with
            {
                EffectiveProposalLineage = DeveloperProposalLineage.HumanReviewCorrection
            },
            HumanReviewCorrection = new HumanReviewCorrectionEvidence
            {
                JobId = JobId,
                RunId = RunId,
                Decision = HumanReviewDecision.ChangesRequired,
                RequiredCorrections =
                [
                    new HumanReviewRequiredCorrection
                    {
                        RelativePath = "src/corrected.cs",
                        Instruction = "Use the corrected proposal."
                    }
                ],
                RecordedAtUtc = DateTimeOffset.UtcNow
            },
            DeveloperProposal = new ValidatedDeveloperProposal(
                "Stale original",
                [
                    new ValidatedDeveloperChange(
                        DeveloperChangeOperationType.CreateFile,
                        "src/original.cs",
                        "Stale",
                        "stale content",
                        string.Empty,
                        13)
                ],
                [], [], 13, 100)
        };
        var ai = new FakeAi { Response = Success(ReviewJson("Approved")) };
        var store = new OrderedStore();

        ReviewerExecutionResult result = await new ReviewerExecutionService(
            ai, store, Options()).ExecuteAsync(request);

        Assert.Equal(ReviewerExecutionFailureKind.InvalidRequest,
            result.FailureKind);
        Assert.Null(ai.Request);
        Assert.Empty(store.Types);
    }

    [Fact]
    public void Policy_approves_only_when_all_deterministic_gates_pass()
    {
        var policy = new ReviewDecisionPolicy();
        Assert.Equal(ReviewerDecision.Approved, policy.Evaluate(Input(ReviewerDecision.Approved, true, true, true)).Decision);
        Assert.False(policy.Evaluate(Input(ReviewerDecision.Approved, false, true, true)).IsSuccess);
        Assert.False(policy.Evaluate(Input(ReviewerDecision.Approved, true, false, true)).IsSuccess);
        Assert.False(policy.Evaluate(Input(ReviewerDecision.Approved, true, true, false)).IsSuccess);
        Assert.Equal(ReviewerDecision.ChangesRequired, policy.Evaluate(Input(ReviewerDecision.ChangesRequired, true, true, true)).Decision);
        Assert.Equal(ReviewerDecision.Rejected, policy.Evaluate(Input(ReviewerDecision.Rejected, true, true, true)).Decision);
    }

    private static ReviewDecisionInput Input(ReviewerDecision decision, bool buildOk, bool testOk, bool observedOk)
    {
        Guid observedJob = observedOk ? JobId : Guid.NewGuid();
        return new(JobId, RunId, Review(decision), Build(buildOk), Test(testOk),
            new(observedJob, RunId, new string('a',40), []));
    }

    private static ReviewerExecutionRequest Request() => new()
    {
        JobId=JobId, RunId=RunId, JobRequest="request", Plan=new()
        {
            Objective="objective", FilesToInspect=[], CandidateFilesToModify=["src/file.cs"], Strategy="strategy",
            AcceptanceCriteria=[], Risks=[], ExpectedTests=[], Assumptions=[], Uncertainties=[]
        },
        DeveloperProposal=new("summary",[],[],[],0,10),
        EffectiveProposalLineage=DeveloperProposalLineage.Original,
        EffectiveSourceSnapshot=new(
            JobId, RunId, DeveloperProposalLineage.Original,
            [Source("src/file.cs", "final source")]),
        ObservedChanges=new(JobId,RunId,new string('a',40),[]),
        BuildReport=Build(true), TestReport=Test(true), CorrelationId="corr"
    };

    private static ReviewerReview Review(ReviewerDecision d) => new()
    { Decision=d, Findings=[], RequiredCorrections=[], RiskAssessment="low", Summary="ok" };
    private static BuildExecutionReport Build(bool ok) => new(JobId,RunId,"target",
        ok ? ToolExecutionOutcome.Completed : ToolExecutionOutcome.NonZeroExitCode, ok ? 0 : 1, "",DateTime.UtcNow,DateTime.UtcNow,TimeSpan.Zero);
    private static TestExecutionReport Test(bool ok) => new(JobId,RunId,"target",
        ok ? ToolExecutionOutcome.Completed : ToolExecutionOutcome.NonZeroExitCode, ok ? 0 : 1, "",DateTime.UtcNow,DateTime.UtcNow,TimeSpan.Zero);
    private static string ReviewJson(string d) =>
        "{\"decision\":\""+d+"\",\"findings\":[],\"requiredCorrections\":[],\"riskAssessment\":\"low\",\"summary\":\"ok\"}";
    private static ReviewerEffectiveSourceFile Source(string path, string content) =>
        new(path,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)))
                .ToLowerInvariant(),
            Encoding.UTF8.GetByteCount(content),
            content);
    private static AiResponse Success(string content) => new()
    { Status=AiOperationStatus.Success, Content=content, Provider="fake", LogicalModel="CodingQuality",
      PhysicalModel="fake", TerminationReason=AiTerminationReason.Stop };
    private static AiGatewayOptions Options() => new()
    {
        Provider="Ollama", Endpoint="http://localhost", Models=new Dictionary<string,string>
        { ["CodingFast"]="a",["CodingQuality"]="b",["General"]="c" }, MaxConcurrentInferences=1,
        ConnectionTimeout=TimeSpan.FromSeconds(1),InferenceTimeout=TimeSpan.FromSeconds(1),
        QueueWaitTimeout=TimeSpan.FromSeconds(1),MaxOutputTokens=1000,MaxInputCharacters=100000,MaxResponseBytes=100000
    };

    private sealed class FakeAi : IAiGateway
    {
        public required AiResponse Response { get; init; }
        public AiRequest? Request { get; private set; }
        public Task<AiResponse> GenerateAsync(AiRequest request,CancellationToken cancellationToken=default)
        { Request=request; return Task.FromResult(Response); }
        public Task<AiProviderHealthResult> CheckHealthAsync(CancellationToken cancellationToken=default) =>
            throw new NotSupportedException();
    }
    private sealed class OrderedStore : IArtifactStore
    {
        public List<ArtifactType> Types { get; }=[];
        public Task<ArtifactWriteResult> WriteAsync(ArtifactWriteRequest request,CancellationToken cancellationToken=default)
        {
            Types.Add(request.ArtifactType);
            return Task.FromResult(ArtifactWriteResult.Success(new ArtifactRecord
            { ArtifactId=Guid.NewGuid(),JobId=request.JobId,RunId=request.RunId,ArtifactType=request.ArtifactType,
              RelativePath="review/test.json",Sha256=new string('a',64),SizeBytes=request.Content.Length,
              CreatedAtUtc=DateTimeOffset.UtcNow,CorrelationId=request.CorrelationId }));
        }
    }
}

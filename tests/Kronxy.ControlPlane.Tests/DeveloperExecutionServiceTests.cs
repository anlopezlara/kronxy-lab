using System.Security.Cryptography;
using System.Text;
using Kronxy.Application.AI;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Context;
using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;
using Kronxy.Infrastructure.AI;
using Kronxy.Infrastructure.Artifacts;
using Kronxy.Infrastructure.Execution;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class DeveloperExecutionServiceTests
{
    [Fact]
    public async Task Valid_output_is_validated_and_persisted_in_safe_order()
    {
        Fixture fixture = CreateFixture();

        DeveloperExecutionResult result =
            await fixture.Service.ExecuteAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Proposal);
        Assert.NotNull(fixture.Gateway.LastRequest!.StructuredOutput);
        Assert.Equal(
            AiLogicalModel.General,
            fixture.Gateway.LastRequest.Model);

        Assert.Equal(
            TimeSpan.FromSeconds(90),
            fixture.Gateway.LastRequest.InferenceTimeout);

        Assert.Contains(
            "smallest complete JSON proposal",
            fixture.Gateway.LastRequest.SystemInstructions);
        Assert.Contains(
            "never pad content with repeated blank lines",
            fixture.Gateway.LastRequest.SystemInstructions);
        Assert.Contains(
            "Keep combined file content under 6000 characters",
            fixture.Gateway.LastRequest.SystemInstructions);
        Assert.Contains(
            "Each change content must contain only the file named by relativePath",
            fixture.Gateway.LastRequest.SystemInstructions);
        Assert.Equal(1, fixture.Policy.CallCount);
        Assert.Collection(
            fixture.Store.Requests,
            item => Assert.Equal(
                ArtifactType.DeveloperProposal,
                item.ArtifactType),
            item => Assert.Equal(
                ArtifactType.DeveloperResponse,
                item.ArtifactType));
    }

    [Fact]
    public async Task Developer_budget_reserves_required_sections_and_prioritizes_plan_paths()
    {
        Fixture fixture = CreateFixture();

        DeveloperExecutionResult result =
            await fixture.Service.ExecuteAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.Equal(48_000, fixture.Context.LastRequest!.MaxCharacters);
        Assert.Contains("src/a.cs", fixture.Context.LastRequest.PriorityPaths);
        Assert.DoesNotContain(
            "src/new.cs",
            fixture.Context.LastRequest.PriorityPaths);

        AiRequest sent = Assert.IsType<AiRequest>(fixture.Gateway.LastRequest);
        Assert.Contains("Implement safely.", sent.UserContent);
        Assert.Contains("Minimal change.", sent.UserContent);
        int contextIndex =
            sent.UserContent.IndexOf(
                "===== FILE: src/a.cs =====",
                StringComparison.Ordinal);
        int finalRequirementsIndex =
            sent.UserContent.IndexOf(
                "FINAL OUTPUT REQUIREMENTS:",
                StringComparison.Ordinal);
        Assert.True(finalRequirementsIndex > contextIndex);
        Assert.Contains(
            "Do not repeat the repository, plan, request, or code.",
            sent.UserContent);
        Assert.Contains(
            "Each change content must contain exactly one file.",
            sent.UserContent);
        Assert.Contains("- src/new.cs", sent.UserContent);
        Assert.DoesNotContain(
            "- src/a.cs",
            sent.UserContent[finalRequirementsIndex..]);
        Assert.NotNull(sent.StructuredOutput);

        long total = (long)sent.SystemInstructions.Length +
            sent.UserContent.Length +
            sent.StructuredOutput!.Schema.GetRawText().Length;
        Assert.True(total <= 65_536);
    }

    [Fact]
    public void Developer_context_budget_at_hard_limit_is_rejected() =>
        Assert.Throws<InvalidOperationException>(
            () => CreateFixture(65_536));

    [Fact]
    public async Task Oversize_reviewer_feedback_fails_before_context_and_ai()
    {
        Fixture fixture = CreateFixture();
        var feedback = new ReviewerReview
        {
            Decision = ReviewerDecision.ChangesRequired,
            Findings = [],
            RequiredCorrections = [],
            RiskAssessment = "risk",
            Summary = new string((char)120, 9_000)
        };

        DeveloperExecutionResult result =
            await fixture.Service.ExecuteAsync(
                Request() with { ReviewerFeedback = feedback });

        Assert.Equal(
            DeveloperExecutionFailureKind.ContextTooLarge,
            result.FailureKind);
        Assert.Equal(
            "DEVELOPER_REVIEWER_FEEDBACK_LIMIT_EXCEEDED",
            result.ErrorCode);
        Assert.Null(fixture.Context.LastRequest);
        Assert.Equal(0, fixture.Gateway.CallCount);
    }

    [Fact]
    public async Task Invalid_json_fails_without_policy_or_artifacts()
    {
        Fixture fixture = CreateFixture();
        fixture.Gateway.Response = SuccessResponse("{not-json");

        DeveloperExecutionResult result =
            await fixture.Service.ExecuteAsync(Request());

        Assert.Equal(
            DeveloperExecutionFailureKind.AiInvalidResponse,
            result.FailureKind);
        Assert.Equal(0, fixture.Policy.CallCount);
        Assert.Empty(fixture.Store.Requests);
    }

    [Fact]
    public async Task Incomplete_provider_response_fails_closed_and_persists_safe_evidence()
    {
        Fixture fixture = CreateFixture();
        fixture.Gateway.Response = new AiResponse
        {
            Status = AiOperationStatus.InvalidResponse,
            Content = "partial proposal",
            Provider = "Ollama",
            LogicalModel = "CodingQuality",
            PhysicalModel = "quality",
            Duration = TimeSpan.FromSeconds(83),
            TerminationReason = AiTerminationReason.Length,
            Usage = new AiUsage(5000, 2048),
            ProviderMetadata = new AiProviderResponseMetadata
            {
                Done = false,
                DoneReason = "length",
                PromptEvalCount = 5000,
                EvalCount = 2048
            },
            ErrorCode = "OLLAMA_INCOMPLETE_RESPONSE"
        };

        DeveloperExecutionResult result =
            await fixture.Service.ExecuteAsync(Request());

        Assert.Equal(
            DeveloperExecutionFailureKind.AiInvalidResponse,
            result.FailureKind);
        Assert.Equal("OLLAMA_INCOMPLETE_RESPONSE", result.ErrorCode);
        Assert.Equal(0, fixture.Policy.CallCount);

        ArtifactWriteRequest evidence =
            Assert.Single(fixture.Store.Requests);
        Assert.Equal(
            ArtifactType.DeveloperRejectedResponse,
            evidence.ArtifactType);

        string json = Encoding.UTF8.GetString(evidence.Content.Span);
        Assert.Contains("partial proposal", json);
        Assert.Contains("\"Done\":false", json);
        Assert.Contains("\"DoneReason\":\"length\"", json);
        Assert.DoesNotContain("Authorization", json);
        Assert.DoesNotContain("ConnectionStrings", json);
        Assert.DoesNotContain("Password", json);
    }

    [Fact]
    public async Task Invalid_structured_response_uses_distinct_evidence_artifact()
    {
        Fixture fixture = CreateFixture();
        fixture.Gateway.Response = new AiResponse
        {
            Status = AiOperationStatus.InvalidResponse,
            Content = "{invalid",
            Provider = "Ollama",
            LogicalModel = "CodingFast",
            PhysicalModel = "fast",
            ErrorCode = "AI_STRUCTURED_INVALID_JSON",
            TerminationReason = AiTerminationReason.Error,
            Usage = new AiUsage(5_547, 2_048),
            ProviderMetadata = new AiProviderResponseMetadata
            {
                Done = true,
                DoneReason = "length",
                PromptEvalCount = 5_547,
                EvalCount = 2_048
            }
        };

        DeveloperExecutionResult result =
            await fixture.Service.ExecuteAsync(Request());

        Assert.Equal(
            DeveloperExecutionFailureKind.AiInvalidResponse,
            result.FailureKind);

        Assert.Equal(
            ArtifactType.DeveloperRejectedStructuredResponse,
            Assert.Single(fixture.Store.Requests).ArtifactType);

        string evidence = Encoding.UTF8.GetString(
            fixture.Store.Requests[0].Content.Span);
        Assert.Contains("{invalid", evidence);
        Assert.Contains("\"Done\":true", evidence);
        Assert.Contains("\"DoneReason\":\"length\"", evidence);
        Assert.Contains("\"PromptEvalCount\":5547", evidence);
        Assert.Contains("\"EvalCount\":2048", evidence);
    }

    [Fact]
    public async Task Structurally_invalid_output_is_not_persisted()
    {
        Fixture fixture = CreateFixture();
        fixture.Gateway.Response = SuccessResponse(
            """
            { "summary": "missing required members" }
            """);

        DeveloperExecutionResult result =
            await fixture.Service.ExecuteAsync(Request());

        Assert.Equal(
            DeveloperExecutionFailureKind.AiInvalidResponse,
            result.FailureKind);
        Assert.Equal(0, fixture.Policy.CallCount);
        Assert.Empty(fixture.Store.Requests);
    }

    [Fact]
    public async Task Policy_rejection_is_fail_closed()
    {
        Fixture fixture = CreateFixture();
        fixture.Policy.Result =
            DeveloperProposalPolicyResult.Failure(
                DeveloperProposalFailureKind.ProtectedPath,
                "DEVELOPER_PATH_PROTECTED");

        DeveloperExecutionResult result =
            await fixture.Service.ExecuteAsync(Request());

        Assert.Equal(
            DeveloperExecutionFailureKind.PolicyRejected,
            result.FailureKind);
        AssertRejectedArtifact(
            fixture,
            ArtifactType.DeveloperRejectedResponse,
            "DEVELOPER_PATH_PROTECTED");
    }

    [Fact]
    public async Task Proposal_paths_outside_planner_allowlist_are_rejected()
    {
        Fixture fixture = CreateFixture();
        fixture.Gateway.Response = SuccessResponse(
            """
            {
              "summary":"Create three files.",
              "changes":[
                {"operation":"CreateFile","relativePath":"src/outside-a.cs","intent":"Create A.","content":"class A {}","expectedContentSha256":""},
                {"operation":"CreateFile","relativePath":"src/outside-b.cs","intent":"Create B.","content":"class B {}","expectedContentSha256":""},
                {"operation":"CreateFile","relativePath":"src/outside-c.cs","intent":"Create C.","content":"class C {}","expectedContentSha256":""}
              ],
              "assumptions":[],
              "risks":[]
            }
            """);

        DeveloperExecutionResult result =
            await fixture.Service.ExecuteAsync(Request());

        Assert.Equal(
            DeveloperExecutionFailureKind.PolicyRejected,
            result.FailureKind);
        Assert.Equal("DEVELOPER_PATH_NOT_IN_PLAN", result.ErrorCode);
        Assert.Equal(0, fixture.Policy.CallCount);
        AssertRejectedArtifact(
            fixture,
            ArtifactType.DeveloperRejectedResponse,
            "DEVELOPER_PATH_NOT_IN_PLAN");
    }

    [Fact]
    public async Task Planning_plan_must_be_valid_before_ai_call()
    {
        Fixture fixture = CreateFixture();
        fixture.Reader.PlanContent = "{}"u8.ToArray();

        DeveloperExecutionResult result =
            await fixture.Service.ExecuteAsync(Request());

        Assert.Equal(
            DeveloperExecutionFailureKind.PlanningPlanInvalid,
            result.FailureKind);
        Assert.Equal(0, fixture.Gateway.CallCount);
    }

    [Fact]
    public async Task Planning_plan_must_match_job_request_before_ai_call()
    {
        Fixture fixture =
            CreateFixture();

        fixture.Reader.PlanContent =
            """
            {
              "objective":"Create a new projects API.",
              "filesToInspect":["src/projects.cs"],
              "candidateFilesToModify":["src/projects.cs"],
              "strategy":"Create unrelated project functionality.",
              "acceptanceCriteria":[],
              "risks":[],
              "expectedTests":[],
              "assumptions":[],
              "uncertainties":[]
            }
            """u8.ToArray();

        DeveloperExecutionResult result =
            await fixture.Service.ExecuteAsync(
                Request());

        Assert.Equal(
            DeveloperExecutionFailureKind.PlanningPlanInvalid,
            result.FailureKind);

        Assert.Equal(
            "DEVELOPER_PLAN_POLICY_INVALID",
            result.ErrorCode);

        Assert.Equal(
            0,
            fixture.Gateway.CallCount);

        Assert.Null(
            fixture.Context.LastRequest);

        Assert.Empty(
            fixture.Store.Requests);
    }

    [Fact]
    public async Task Proposal_artifact_failure_prevents_response_artifact()
    {
        Fixture fixture = CreateFixture();
        fixture.Store.Result = ArtifactWriteResult.Failure(
            ArtifactStoreFailureKind.IoFailure,
            "WRITE_FAILED");

        DeveloperExecutionResult result =
            await fixture.Service.ExecuteAsync(Request());

        Assert.Equal(
            DeveloperExecutionFailureKind.ArtifactWriteFailure,
            result.FailureKind);
        Assert.Single(fixture.Store.Requests);
        Assert.Equal(
            ArtifactType.DeveloperProposal,
            fixture.Store.Requests[0].ArtifactType);
    }

    [Fact]
    public async Task Build_correction_receives_compiler_evidence_and_preserves_original_artifacts()
    {
        Fixture fixture = CreateFixture();
        DeveloperExecutionRequest request = BuildCorrectionRequest();
        string hash = CurrentHash("class NewType {}");
        fixture.MetadataBinder.ReplaceHash = hash;
        fixture.Gateway.Response = SuccessResponse(
            $$"""
            {
              "summary":"Fix the compiler error.",
              "changes":[{
                "operation":"ReplaceFile",
                "relativePath":"src/new.cs",
                "intent":"Add the missing namespace import.",
                "content":"using Missing.Namespace;\nclass NewType {}",
                "expectedContentSha256":"not-a-valid-ai-hash"
              }],
              "assumptions":[],
              "risks":[]
            }
            """);

        DeveloperExecutionResult result =
            await fixture.Service.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Contains("CS0246", fixture.Gateway.LastRequest!.UserContent);
        Assert.Equal(hash,
            Assert.Single(result.Proposal!.Changes).ExpectedContentSha256);
        Assert.Contains(
            "previous proposal failed Build",
            fixture.Gateway.LastRequest.SystemInstructions);
        Assert.Contains(
            "KRONXY binds the governed workspace hash",
            fixture.Gateway.LastRequest.SystemInstructions);
        Assert.Collection(
            fixture.Store.Requests,
            item => Assert.Equal(
                ArtifactType.DeveloperBuildCorrectionProposal,
                item.ArtifactType),
            item => Assert.Equal(
                ArtifactType.DeveloperBuildCorrectionResponse,
                item.ArtifactType));
    }

    [Fact]
    public async Task Build_correction_outside_original_plan_fails_closed()
    {
        Fixture fixture = CreateFixture();
        DeveloperExecutionRequest request = BuildCorrectionRequest();
        fixture.Gateway.Response = SuccessResponse(
            """
            {
              "summary":"Expand the correction.",
              "changes":[{
                "operation":"ReplaceFile",
                "relativePath":"src/outside.cs",
                "intent":"Touch an unauthorized file.",
                "content":"class Outside {}",
                "expectedContentSha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
              }],
              "assumptions":[],
              "risks":[]
            }
            """);

        DeveloperExecutionResult result =
            await fixture.Service.ExecuteAsync(request);

        Assert.Equal(
            DeveloperExecutionFailureKind.PolicyRejected,
            result.FailureKind);
        Assert.Equal("DEVELOPER_PATH_NOT_IN_PLAN", result.ErrorCode);
        AssertRejectedArtifact(
            fixture,
            ArtifactType.DeveloperBuildCorrectionRejectedResponse,
            "DEVELOPER_PATH_NOT_IN_PLAN");
    }

    [Fact]
    public async Task Build_correction_cannot_create_an_existing_file_again()
    {
        Fixture fixture = CreateFixture();
        fixture.Gateway.Response = SuccessResponse(
            """
            {
              "summary":"Create the file again.",
              "changes":[{
                "operation":"CreateFile",
                "relativePath":"src/new.cs",
                "intent":"Incorrect correction operation.",
                "content":"class NewType {}",
                "expectedContentSha256":""
              }],
              "assumptions":[],
              "risks":[]
            }
            """);

        DeveloperExecutionResult result =
            await fixture.Service.ExecuteAsync(BuildCorrectionRequest());

        Assert.Equal(
            DeveloperExecutionFailureKind.PolicyRejected,
            result.FailureKind);
        Assert.Equal(
            "DEVELOPER_BUILD_CORRECTION_INVALID",
            result.ErrorCode);
        AssertRejectedArtifact(
            fixture,
            ArtifactType.DeveloperBuildCorrectionRejectedResponse,
            "DEVELOPER_BUILD_CORRECTION_INVALID");
    }

    [Fact]
    public async Task Human_review_correction_consumes_finding_and_writes_separate_artifacts()
    {
        Fixture fixture = CreateFixture();
        string hash = CurrentHash("class NewType {}");
        fixture.MetadataBinder.ReplaceHash = hash;
        fixture.Gateway.Response = SuccessResponse(
            $$"""
            {"summary":"Apply human finding.","changes":[{"operation":"ReplaceFile","relativePath":"src/new.cs","intent":"Add required behavior.","content":"class NewType { public int Value { get; } }","expectedContentSha256":"invalid-ai-hash"}],"assumptions":[],"risks":[]}
            """);

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(
            HumanReviewCorrectionRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(hash,
            Assert.Single(result.Proposal!.Changes).ExpectedContentSha256);
        Assert.Contains("\"HumanReviewDecision\":10", fixture.Gateway.LastRequest!.UserContent);
        Assert.Contains("Add the required Value member", fixture.Gateway.LastRequest.UserContent);
        Assert.Contains("Human Review returned ChangesRequired",
            fixture.Gateway.LastRequest.SystemInstructions);
        Assert.Collection(fixture.Store.Requests,
            item => Assert.Equal(ArtifactType.DeveloperHumanReviewCorrectionProposal, item.ArtifactType),
            item => Assert.Equal(ArtifactType.DeveloperHumanReviewCorrectionResponse, item.ArtifactType));
    }

    [Fact]
    public async Task Human_review_correction_outside_plan_fails_closed()
    {
        Fixture fixture = CreateFixture();
        fixture.Gateway.Response = SuccessResponse(
            """
            {"summary":"Escape scope.","changes":[{"operation":"ReplaceFile","relativePath":"src/outside.cs","intent":"Unauthorized.","content":"class Outside {}","expectedContentSha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}],"assumptions":[],"risks":[]}
            """);

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(
            HumanReviewCorrectionRequest());

        Assert.Equal(DeveloperExecutionFailureKind.PolicyRejected, result.FailureKind);
        Assert.Equal("DEVELOPER_PATH_NOT_IN_PLAN", result.ErrorCode);
        AssertRejectedArtifact(
            fixture,
            ArtifactType.DeveloperHumanReviewCorrectionRejectedResponse,
            "DEVELOPER_PATH_NOT_IN_PLAN");
    }

    [Fact]
    public async Task Human_review_correction_cannot_create_existing_file()
    {
        Fixture fixture = CreateFixture();
        fixture.Gateway.Response = SuccessResponse(
            """
            {"summary":"Recreate file.","changes":[{"operation":"CreateFile","relativePath":"src/new.cs","intent":"Invalid operation.","content":"class NewType {}","expectedContentSha256":""}],"assumptions":[],"risks":[]}
            """);

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(
            HumanReviewCorrectionRequest());

        Assert.Equal(DeveloperExecutionFailureKind.PolicyRejected, result.FailureKind);
        Assert.Equal("DEVELOPER_HUMAN_REVIEW_CORRECTION_INVALID", result.ErrorCode);
        AssertRejectedArtifact(
            fixture,
            ArtifactType.DeveloperHumanReviewCorrectionRejectedResponse,
            "DEVELOPER_HUMAN_REVIEW_CORRECTION_INVALID");
    }

    private static void AssertRejectedArtifact(
        Fixture fixture,
        ArtifactType expectedType,
        string errorCode)
    {
        ArtifactWriteRequest artifact =
            Assert.Single(fixture.Store.Requests);
        Assert.Equal(expectedType, artifact.ArtifactType);
        string json = Encoding.UTF8.GetString(artifact.Content.Span);
        Assert.Contains($"\"PolicyErrorCode\":\"{errorCode}\"", json);
        Assert.Contains("\"PhysicalModel\":\"quality\"", json);
        Assert.DoesNotContain("Authorization", json);
        Assert.DoesNotContain("Password", json);
    }

    private static DeveloperExecutionRequest Request()
    {
        Guid jobId = Guid.NewGuid();
        return new DeveloperExecutionRequest
        {
            JobId = jobId,
            RunId = Guid.NewGuid(),
            JobRequest = "Implement safely.",
            CorrelationId = "corr-developer",
            Repository = new RepositoryWorktreeHandle(
                jobId,
                "KRX-TEST",
                "/workspace/job",
                "/workspace/job/repository",
                "kronxy/jobs/test",
                new string('a', 40),
                RepositoryWorktreeOperationKind.Existing)
        };
    }

    private static DeveloperExecutionRequest BuildCorrectionRequest()
    {
        DeveloperExecutionRequest request = Request();
        var original = new ValidatedDeveloperProposal(
            "Create a file.",
            [new ValidatedDeveloperChange(
                DeveloperChangeOperationType.CreateFile,
                "src/new.cs",
                "Implement the requested behavior.",
                "class NewType {}",
                string.Empty,
                16)],
            [],
            [],
            16,
            100);

        return request with
        {
            BuildCorrection = new DeveloperBuildCorrectionContext
            {
                OriginalProposal = original,
                FailedBuildReport = new BuildExecutionReport(
                    request.JobId,
                    request.RunId,
                    "Kronxy.sln",
                    ToolExecutionOutcome.NonZeroExitCode,
                    1,
                    "TOOL_NONZERO_EXIT",
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                    TimeSpan.FromSeconds(2)),
                BuildStandardOutput =
                    "src/new.cs(1,1): error CS0246: Missing type"
            }
        };
    }

    private static DeveloperExecutionRequest HumanReviewCorrectionRequest()
    {
        DeveloperExecutionRequest request = Request();
        var current = new ValidatedDeveloperProposal(
            "Create a file.",
            [new ValidatedDeveloperChange(
                DeveloperChangeOperationType.CreateFile,
                "src/new.cs", "Implement behavior.",
                "class NewType {}", string.Empty, 16)],
            [], [], 16, 100);
        var build = new BuildExecutionReport(
            request.JobId, request.RunId, "Kronxy.sln",
            ToolExecutionOutcome.Completed, 0, string.Empty,
            DateTime.UtcNow, DateTime.UtcNow, TimeSpan.FromSeconds(1));
        var test = new TestExecutionReport(
            request.JobId, request.RunId, "Kronxy.sln",
            ToolExecutionOutcome.Completed, 0, string.Empty,
            DateTime.UtcNow, DateTime.UtcNow, TimeSpan.FromSeconds(1));
        var review = new ReviewerReview
        {
            Decision = ReviewerDecision.Approved,
            Findings = [], RequiredCorrections = [],
            RiskAssessment = "low", Summary = "approved"
        };
        var evidence = new HumanReviewCorrectionEvidence
        {
            JobId = request.JobId, RunId = request.RunId,
            Decision = HumanReviewDecision.ChangesRequired,
            RequiredCorrections =
            [
                new HumanReviewRequiredCorrection
                {
                    RelativePath = "src/new.cs",
                    Instruction = "Add the required Value member."
                }
            ],
            RecordedAtUtc = DateTimeOffset.UtcNow
        };

        return request with
        {
            HumanReviewCorrection = new DeveloperHumanReviewCorrectionContext
            {
                CurrentProposal = current,
                BuildReport = build,
                TestReport = test,
                ReviewerReview = review,
                HumanReviewEvidence = evidence
            }
        };
    }

    private static string CurrentHash(string content) =>
        Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(content)))
            .ToLowerInvariant();

    private static Fixture CreateFixture(int developerContextCharacters = 48_000)
    {
        var reader = new FakeReader();
        var context = new FakeContextBuilder();
        var gateway = new FakeGateway();
        var policy = new FakePolicy();
        var metadataBinder = new FakeMetadataBinder();
        var store = new FakeStore();
        var artifactOptions = new ArtifactStoreOptions
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "kronxy-developer-tests"),
            MaxArtifactBytes = 16_777_216
        };
        var aiOptions = new AiGatewayOptions
        {
            Provider = "Ollama",
            Endpoint = "http://localhost:11434",
            Models = new Dictionary<string, string>
            {
                ["CodingFast"] = "fast",
                ["CodingQuality"] = "quality",
                ["General"] = "general"
            },
            MaxConcurrentInferences = 2,
            ConnectionTimeout = TimeSpan.FromSeconds(1),
            InferenceTimeout = TimeSpan.FromSeconds(60),
            DeveloperInferenceTimeout = TimeSpan.FromSeconds(90),
            QueueWaitTimeout = TimeSpan.FromSeconds(1),
            MaxOutputTokens = 4_096,
            MaxInputCharacters = 65_536,
            DeveloperContextCharacters = developerContextCharacters,
            DeveloperFeedbackCharacters = 8_192,
            MaxResponseBytes = 1_048_576
        };

        return new Fixture(
            new DeveloperExecutionService(
                reader, context, gateway, policy, metadataBinder, store,
                artifactOptions, aiOptions),
            reader, context, gateway, policy, metadataBinder, store);
    }

    private static AiResponse SuccessResponse(string content) => new()
    {
        Status = AiOperationStatus.Success,
        Content = content,
        Provider = "Fake",
        LogicalModel = "CodingQuality",
        PhysicalModel = "quality",
        TerminationReason = AiTerminationReason.Stop,
        Usage = new AiUsage(10, 20)
    };

    private sealed record Fixture(
        DeveloperExecutionService Service,
        FakeReader Reader,
        FakeContextBuilder Context,
        FakeGateway Gateway,
        FakePolicy Policy,
        FakeMetadataBinder MetadataBinder,
        FakeStore Store);

    private sealed class FakeMetadataBinder :
        IDeveloperProposalMetadataBinder
    {
        public int CallCount { get; private set; }
        public DeveloperProposalMetadataBindingRequest? LastRequest { get; private set; }
        public string ReplaceHash { get; set; } = new string('b', 64);
        public DeveloperProposalMetadataBindingResult? Result { get; set; }

        public Task<DeveloperProposalMetadataBindingResult> BindAsync(
            DeveloperProposalMetadataBindingRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;

            if (Result is not null)
                return Task.FromResult(Result);

            if (request.Proposal.Changes.Any(change =>
                    !request.AllowedPaths.Contains(
                        change.RelativePath,
                        StringComparer.OrdinalIgnoreCase)))
            {
                return Task.FromResult(
                    DeveloperProposalMetadataBindingResult.Failure(
                        "DEVELOPER_PATH_NOT_IN_PLAN"));
            }

            DeveloperChangeOperation[] changes = request.Proposal.Changes
                .Select(change => change with
                {
                    ExpectedContentSha256 =
                        change.Operation == DeveloperChangeOperationType.ReplaceFile
                            ? ReplaceHash
                            : string.Empty
                })
                .ToArray();

            return Task.FromResult(
                DeveloperProposalMetadataBindingResult.Success(
                    request.Proposal with { Changes = changes }));
        }
    }

    private sealed class FakeReader : IArtifactReader
    {
        public byte[] PlanContent { get; set; } =
            """
            {
              "objective":"Implement safely.",
              "filesToInspect":["src/a.cs"],
              "candidateFilesToModify":["src/new.cs"],
              "strategy":"Minimal change.",
              "acceptanceCriteria":["Build succeeds."],
              "risks":[],"expectedTests":[],"assumptions":[],"uncertainties":[]
            }
            """u8.ToArray();

        public Task<ArtifactReadResult> ReadAsync(
            ArtifactReadRequest request,
            CancellationToken cancellationToken = default)
        {
            byte[] content = request.ArtifactType == ArtifactType.PlanningPlan
                ? PlanContent
                : new byte[] { 1, 2, 3 };
            return Task.FromResult(ArtifactReadResult.Success(
                Artifact(request, content.Length), content));
        }

        private static ArtifactRecord Artifact(
            ArtifactReadRequest request,
            int size) => new()
            {
                ArtifactId = Guid.NewGuid(),
                JobId = request.JobId,
                RunId = request.RunId,
                ArtifactType = request.ArtifactType,
                RelativePath = "test/artifact",
                Sha256 = new string('a', 64),
                SizeBytes = size,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CorrelationId = request.CorrelationId
            };
    }

    private sealed class FakeContextBuilder : IContextAiInputBuilder
    {
        public ContextAiInputRequest? LastRequest { get; private set; }

        public Task<ContextAiInputResult> BuildAsync(
            ContextAiInputRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(ContextAiInputResult.Success(
                "===== FILE: src/a.cs =====\nclass A {}"));
        }
    }

    private sealed class FakeGateway : IAiGateway
    {
        public int CallCount { get; private set; }
        public AiRequest? LastRequest { get; private set; }
        public AiResponse Response { get; set; } = SuccessResponse(
            """
            {
              "summary":"Create a file.",
              "changes":[{
                "operation":"CreateFile",
                "relativePath":"src/new.cs",
                "intent":"Implement the requested behavior.",
                "content":"class NewType {}",
                "expectedContentSha256":""
              }],
              "assumptions":[],
              "risks":[]
            }
            """);

        public Task<AiResponse> GenerateAsync(
            AiRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(Response);
        }

        public Task<AiProviderHealthResult> CheckHealthAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakePolicy : IDeveloperProposalPolicy
    {
        public int CallCount { get; private set; }
        public DeveloperProposalPolicyResult? Result { get; set; }

        public DeveloperProposalPolicyResult Validate(
            DeveloperProposal? proposal)
        {
            CallCount++;
            if (Result is not null)
                return Result;

            DeveloperChangeOperation[] changes = proposal!.Changes.ToArray();
            return DeveloperProposalPolicyResult.Success(
                new ValidatedDeveloperProposal(
                    proposal.Summary,
                    changes.Select(
                        change => new ValidatedDeveloperChange(
                            change.Operation,
                            change.RelativePath,
                            change.Intent,
                            change.Content,
                            change.ExpectedContentSha256,
                            change.Content.Length))
                        .ToArray(),
                    proposal.Assumptions,
                    proposal.Risks,
                    changes.Sum(change => change.Content.Length),
                    100));
        }
    }

    private sealed class FakeStore : IArtifactStore
    {
        public List<ArtifactWriteRequest> Requests { get; } = new();
        public ArtifactWriteResult? Result { get; set; }

        public Task<ArtifactWriteResult> WriteAsync(
            ArtifactWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(Result ?? ArtifactWriteResult.Success(
                new ArtifactRecord
                {
                    ArtifactId = Guid.NewGuid(),
                    JobId = request.JobId,
                    RunId = request.RunId,
                    ArtifactType = request.ArtifactType,
                    RelativePath = "developer/artifact.json",
                    Sha256 = new string('a', 64),
                    SizeBytes = request.Content.Length,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    CorrelationId = request.CorrelationId
                }));
        }
    }
}

using System.Security.Cryptography;
using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
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

        Assert.True(result.IsSuccess, result.ErrorCode);
        Assert.NotNull(result.Proposal);
        Assert.NotNull(fixture.Gateway.LastRequest!.StructuredOutput);
        Assert.Equal(
            AiLogicalModel.CodingQuality,
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
            "Use ReplaceFile for exists=true",
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

        Assert.True(result.IsSuccess, result.ErrorCode);
        Assert.InRange(
            fixture.Context.LastRequest!.MaxCharacters,
            1,
            47_999);
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
    public async Task Existing_authoritative_target_supplies_content_hash_and_requires_replace()
    {
        const string path = "src/new.cs";
        const string current = "public sealed class Existing {}\n";
        string hash = CurrentHash(current);
        Fixture fixture = CreateFixture();
        fixture.MetadataBinder.ReplaceHash = hash;
        fixture.Reader.ContextContent = ContextPackage((path, current));
        fixture.Gateway.Response = SuccessResponse(
            $$"""
            {"summary":"Modify existing file.","changes":[{"operation":"ReplaceFile","relativePath":"{{path}}","intent":"Preserve and update file.","content":"public sealed class Existing { public int Value { get; } }\n","expectedContentSha256":"{{hash}}"}],"assumptions":[],"risks":[]}
            """);

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(Request());

        Assert.True(result.IsSuccess);
        string input = fixture.Gateway.LastRequest!.UserContent;
        Assert.Contains("AUTHORIZED TARGET FILES", input);
        Assert.Contains("\"Exists\":true", input);
        Assert.Contains(current.Trim(), input);
        Assert.Contains(hash, input);
        Assert.Null(fixture.Context.LastRequest);
        Assert.Equal(
            8_192,
            fixture.Gateway.LastRequest.Generation.MaxOutputTokens);
        Assert.Equal(
            DeveloperChangeOperationType.ReplaceFile,
            Assert.Single(result.Proposal!.Changes).Operation);
    }

    [Fact]
    public async Task CreateFile_for_existing_authoritative_target_is_rejected()
    {
        const string path = "src/new.cs";
        const string current = "public sealed class Existing {}\n";
        Fixture fixture = CreateFixture();
        fixture.Reader.ContextContent = ContextPackage((path, current));

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(Request());

        Assert.False(result.IsSuccess);
        Assert.Equal("DEVELOPER_CREATE_EXISTING_FILE_REJECTED", result.ErrorCode);
    }

    [Fact]
    public async Task Medium_existing_target_receives_dynamically_reduced_output_budget()
    {
        const string path = "src/new.cs";
        string current = string.Concat(Enumerable.Repeat(
            "<div class=\"job-detail\">@Model.JobId & @Model.RunId</div>\n",
            340));
        string hash = CurrentHash(current);
        Fixture fixture = CreateFixture();
        fixture.MetadataBinder.ReplaceHash = hash;
        fixture.Reader.ContextContent = ContextPackage((path, current));
        fixture.Gateway.Response = SuccessResponse(
            $$"""
            {"summary":"Modify existing file.","changes":[{"operation":"ReplaceFile","relativePath":"{{path}}","intent":"Preserve and update file.","content":"updated","expectedContentSha256":"{{hash}}"}],"assumptions":[],"risks":[]}
            """);

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(Request());

        Assert.True(result.IsSuccess);
        int outputTokens = fixture.Gateway.LastRequest!.Generation.MaxOutputTokens!.Value;
        Assert.InRange(outputTokens, 2_048, 8_191);
        Assert.Equal(16_384, fixture.Gateway.LastRequest.Generation.ContextWindowTokens);
    }

    [Fact]
    public async Task Representative_19181_byte_target_fits_without_truncation()
    {
        const string path = "src/new.cs";
        const int targetBytes = 19_181;
        const string fragment =
            "<div class=\"job-detail\">@Model.JobId & @Model.RunId</div>\n";
        string current = string.Concat(Enumerable.Repeat(
            fragment,
            targetBytes / fragment.Length + 1))[..targetBytes];
        string hash = CurrentHash(current);
        Fixture fixture = CreateFixture();
        fixture.MetadataBinder.ReplaceHash = hash;
        fixture.Reader.ContextContent = ContextPackage((path, current));
        fixture.Gateway.Response = SuccessResponse(
            $$"""
            {"summary":"Modify existing file.","changes":[{"operation":"ReplaceFile","relativePath":"{{path}}","intent":"Preserve and update file.","content":"updated","expectedContentSha256":"{{hash}}"}],"assumptions":[],"risks":[]}
            """);

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, fixture.Gateway.CallCount);
        string userContent = fixture.Gateway.LastRequest!.UserContent;
        const string targetMarker = "AUTHORIZED TARGET FILES:\n";
        int targetStart = userContent.IndexOf(
            targetMarker,
            StringComparison.Ordinal) + targetMarker.Length;
        int targetEnd = userContent.IndexOf(
            "\n\nFINAL OUTPUT REQUIREMENTS:",
            targetStart,
            StringComparison.Ordinal);
        using JsonDocument targets = JsonDocument.Parse(
            userContent[targetStart..targetEnd]);
        Assert.Equal(
            current,
            targets.RootElement[0].GetProperty("CurrentContent").GetString());
        Assert.Equal(16_384, fixture.Gateway.LastRequest.Generation.ContextWindowTokens);
        Assert.InRange(
            fixture.Gateway.LastRequest.Generation.MaxOutputTokens!.Value,
            6_870,
            8_192);
        Assert.Equal(targetBytes, Encoding.UTF8.GetByteCount(current));
    }

    [Fact]
    public async Task Two_target_19181_byte_pilot_has_deterministic_headroom()
    {
        const string sourcePath =
            "src/Kronxy.Web/Components/Pages/JobDetail.razor";
        const string testPath =
            "tests/Kronxy.Web.Tests/JobDetailTests.cs";
        const int targetBytes = 19_181;
        const string fragment =
            "<div class=\"job-detail\">@Model.JobId & @Model.RunId</div>\n";
        string current = string.Concat(Enumerable.Repeat(
            fragment,
            targetBytes / fragment.Length + 1))[..targetBytes];
        string hash = CurrentHash(current);
        Fixture fixture = CreateFixture();
        fixture.MetadataBinder.ReplaceHash = hash;
        fixture.Reader.PlanContent = JsonSerializer.SerializeToUtf8Bytes(
            new PlannerPlan
            {
                Objective = "Implement safely.",
                FilesToInspect = [sourcePath],
                CandidateFilesToModify = [sourcePath, testPath],
                Strategy = "Add copy controls and focused tests.",
                AcceptanceCriteria = ["Copy controls work."],
                Risks = [],
                ExpectedTests = [],
                Assumptions = [],
                Uncertainties = []
            });
        fixture.Reader.ContextContent = ContextPackage((sourcePath, current));
        fixture.Gateway.Response = SuccessResponse(
            $$"""
            {"summary":"Modify existing file.","changes":[{"operation":"ReplaceFile","relativePath":"{{sourcePath}}","intent":"Add copy controls.","content":"updated","expectedContentSha256":"{{hash}}"}],"assumptions":[],"risks":[]}
            """);

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(Request());

        Assert.True(result.IsSuccess);
        AiRequest sent = fixture.Gateway.LastRequest!;
        int estimatedInputTokens = (
            sent.SystemInstructions.Length + sent.UserContent.Length +
            sent.StructuredOutput!.Schema.GetRawText().Length + 2) / 3;
        int minimumOutputTokens = (
            JsonSerializer.Serialize(
                current,
                new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }).Length + 512 + 2 * 256 + 2) / 3;
        int headroom = 16_384 - 512 - estimatedInputTokens - minimumOutputTokens;

        Assert.True(estimatedInputTokens <= 8_861, $"Input={estimatedInputTokens}");
        Assert.True(minimumOutputTokens >= 6_956, $"Output={minimumOutputTokens}");
        Assert.True(headroom > 0, $"Headroom={headroom}");
        Assert.Equal(16_384, sent.Generation.ContextWindowTokens);
        Assert.Equal(16_384 - 512 - estimatedInputTokens,
            sent.Generation.MaxOutputTokens);
        Assert.DoesNotContain("AUTHORIZED REVIEWER FEEDBACK", sent.UserContent);
        Assert.DoesNotContain("AUTHORIZED CORRECTION EVIDENCE", sent.UserContent);
        Assert.DoesNotContain("AUTHORIZED REPOSITORY CONTEXT", sent.UserContent);
        const string targetMarker = "AUTHORIZED TARGET FILES:\n";
        int targetStart = sent.UserContent.IndexOf(
            targetMarker,
            StringComparison.Ordinal) + targetMarker.Length;
        int targetEnd = sent.UserContent.IndexOf(
            "\n\nFINAL OUTPUT REQUIREMENTS:",
            targetStart,
            StringComparison.Ordinal);
        using JsonDocument targets = JsonDocument.Parse(
            sent.UserContent[targetStart..targetEnd]);
        Assert.Equal(2, targets.RootElement.GetArrayLength());
        Assert.Equal(current,
            targets.RootElement[0].GetProperty("CurrentContent").GetString());
        Assert.Contains(testPath, sent.UserContent);
    }

    [Fact]
    public async Task Mixed_existing_and_new_targets_bind_authoritative_metadata()
    {
        const string sourcePath =
            "src/Kronxy.Web/Components/Pages/JobDetail.razor";
        const string testPath =
            "tests/Kronxy.Web.Tests/JobDetailTests.cs";
        const string current = "<p>@Model.JobId</p>\n";
        string hash = CurrentHash(current);
        Fixture fixture = CreateFixture();
        fixture.MetadataBinder.ReplaceHash = hash;
        fixture.Reader.PlanContent = JsonSerializer.SerializeToUtf8Bytes(
            new PlannerPlan
            {
                Objective = "Implement safely.",
                FilesToInspect = [sourcePath],
                CandidateFilesToModify = [sourcePath, testPath],
                Strategy = "Add controls and tests.",
                AcceptanceCriteria = ["Copy controls work."],
                Risks = [],
                ExpectedTests = [],
                Assumptions = [],
                Uncertainties = []
            });
        fixture.Reader.ContextContent = ContextPackage((sourcePath, current));
        fixture.Gateway.Response = SuccessResponse(
            $$"""
            {"summary":"Add copy controls.","changes":[{"operation":"ReplaceFile","relativePath":"{{sourcePath}}","intent":"Add copy controls.","content":"<p>updated</p>\n","expectedContentSha256":"new_sha256_here"},{"operation":"CreateFile","relativePath":"{{testPath}}","intent":"Add focused tests.","content":"public sealed class JobDetailTests {}\n","expectedContentSha256":"new_sha256_here"}],"assumptions":[],"risks":[]}
            """);

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.Collection(
            result.Proposal!.Changes,
            change =>
            {
                Assert.Equal(sourcePath, change.RelativePath);
                Assert.Equal(hash, change.ExpectedContentSha256);
            },
            change =>
            {
                Assert.Equal(testPath, change.RelativePath);
                Assert.Empty(change.ExpectedContentSha256);
            });
    }

    [Fact]
    public async Task Workspace_drift_is_rejected_before_bound_metadata_is_accepted()
    {
        const string path = "src/new.cs";
        const string authoritative = "class Existing {}\n";
        Fixture fixture = CreateFixture();
        fixture.Reader.ContextContent = ContextPackage((path, authoritative));
        fixture.MetadataBinder.ReplaceHash = CurrentHash("class Drifted {}\n");
        fixture.Gateway.Response = SuccessResponse(
            $$"""
            {"summary":"Modify existing file.","changes":[{"operation":"ReplaceFile","relativePath":"{{path}}","intent":"Update existing file.","content":"class Existing { }\n","expectedContentSha256":"new_sha256_here"}],"assumptions":[],"risks":[]}
            """);

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(Request());

        Assert.False(result.IsSuccess);
        Assert.Equal("DEVELOPER_EXPECTED_CONTENT_SHA_INVALID", result.ErrorCode);
        Assert.Equal(0, fixture.Policy.CallCount);
    }

    [Fact]
    public async Task Placeholder_hash_does_not_bypass_authoritative_target_paths()
    {
        const string path = "src/new.cs";
        Fixture fixture = CreateFixture();
        fixture.Reader.ContextContent = ContextPackage((path, "class Existing {}\n"));
        fixture.Gateway.Response = SuccessResponse(
            """
            {"summary":"Modify outside file.","changes":[{"operation":"ReplaceFile","relativePath":"src/outside.cs","intent":"Modify outside file.","content":"class Outside {}\n","expectedContentSha256":"new_sha256_here"}],"assumptions":[],"risks":[]}
            """);

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(Request());

        Assert.False(result.IsSuccess);
        Assert.Equal("DEVELOPER_PATH_NOT_IN_TARGET_CONTEXT", result.ErrorCode);
        Assert.Equal(0, fixture.MetadataBinder.CallCount);
    }

    [Fact]
    public async Task ReplaceFile_for_missing_authoritative_target_is_rejected()
    {
        Fixture fixture = CreateFixture();
        fixture.Reader.ContextContent = ContextPackage();
        fixture.Gateway.Response = SuccessResponse(
            """
            {"summary":"Replace missing file.","changes":[{"operation":"ReplaceFile","relativePath":"src/new.cs","intent":"Replace missing file.","content":"class NewType {}\n","expectedContentSha256":"new_sha256_here"}],"assumptions":[],"risks":[]}
            """);

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(Request());

        Assert.False(result.IsSuccess);
        Assert.Equal("DEVELOPER_REPLACE_MISSING_FILE_REJECTED", result.ErrorCode);
        Assert.Equal(0, fixture.MetadataBinder.CallCount);
    }

    [Fact]
    public async Task Duplicate_target_operations_are_rejected_before_binding()
    {
        const string path = "src/new.cs";
        Fixture fixture = CreateFixture();
        fixture.Reader.ContextContent = ContextPackage((path, "class Existing {}\n"));
        fixture.Gateway.Response = SuccessResponse(
            $$"""
            {"summary":"Duplicate target changes.","changes":[{"operation":"ReplaceFile","relativePath":"{{path}}","intent":"First target change.","content":"class Existing { }\n","expectedContentSha256":"new_sha256_here"},{"operation":"ReplaceFile","relativePath":"{{path}}","intent":"Second target change.","content":"class Existing { int Value; }\n","expectedContentSha256":"new_sha256_here"}],"assumptions":[],"risks":[]}
            """);

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(Request());

        Assert.False(result.IsSuccess);
        Assert.Equal("DEVELOPER_PATH_DUPLICATE", result.ErrorCode);
        Assert.Equal(0, fixture.MetadataBinder.CallCount);
    }

    [Fact]
    public async Task Impossible_full_file_envelope_fails_before_inference()
    {
        const string path = "src/new.cs";
        string current = new('a', 50_000);
        Fixture fixture = CreateFixture();
        fixture.Reader.ContextContent = ContextPackage((path, current));

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(Request());

        Assert.False(result.IsSuccess);
        Assert.Equal("DEVELOPER_CONTEXT_ENVELOPE_EXCEEDED", result.ErrorCode);
        Assert.Equal(0, fixture.Gateway.CallCount);
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
        Assert.Contains("class NewType {}", fixture.Gateway.LastRequest.UserContent);
        Assert.Equal(1, fixture.Gateway.LastRequest.UserContent.Split("class NewType {}").Length - 1);
        Assert.Contains("- src/new.cs", fixture.Gateway.LastRequest.UserContent);
        Assert.Equal(hash,
            Assert.Single(result.Proposal!.Changes).ExpectedContentSha256);
        Assert.Contains(
            "previous proposal failed Build",
            fixture.Gateway.LastRequest.SystemInstructions);
        Assert.Contains(
            "KRONXY binds the governed workspace hash",
            fixture.Gateway.LastRequest.SystemInstructions);
        Assert.Contains(
            "The compiler diagnostic path is where the inconsistency is observed, not necessarily where the root cause must be fixed.",
            fixture.Gateway.LastRequest.SystemInstructions);
        Assert.Contains(
            "Inspect related declarations before selecting the correction target.",
            fixture.Gateway.LastRequest.UserContent);
        Assert.Contains(
            "consider correcting the declaration rather than rewriting the diagnostic file",
            fixture.Gateway.LastRequest.UserContent);
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
    public async Task Build_correction_compacts_duplicate_errors_and_reaches_ai()
    {
        Fixture fixture = CreateFixture();
        fixture.Gateway.Response = SuccessResponse(
            """
            {"summary":"Fix accessibility.","changes":[{"operation":"ReplaceFile","relativePath":"src/new.cs","intent":"Expose required type.","content":"public class NewType {}","expectedContentSha256":""}],"assumptions":[],"risks":[]}
            """);
        DeveloperExecutionRequest request = BuildCorrectionRequest();
        string error0 =
            "src/new.cs(8,24): error CS0050: Inconsistent accessibility: return type is less accessible";
        string error1 =
            "src/new.cs(9,10): error CS0051: Inconsistent accessibility: parameter type is less accessible";
        string stdout = string.Join('\n',
            Enumerable.Repeat("warning CS0108: unrelated warning", 200)
                .Concat([error0, error1, error0, error1]));
        request = request with
        {
            BuildCorrection = request.BuildCorrection! with
            {
                BuildStandardOutput = stdout
            }
        };

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(request);

        Assert.True(result.IsSuccess, result.ErrorCode);
        Assert.Equal(1, fixture.Gateway.CallCount);
        string input = fixture.Gateway.LastRequest!.UserContent;
        Assert.Contains("CS0050", input);
        Assert.Contains("CS0051", input);
        Assert.DoesNotContain("unrelated warning", input);
        Assert.Contains("\"Count\":2", input);
        Assert.Contains("\"OmittedUniqueDiagnostics\":0", input);
        Assert.Equal(1, input.Split("class NewType {}").Length - 1);
        Assert.True(input.Length < stdout.Length);
        Assert.True(input.Length <= 65_536);
        Assert.Equal(2, fixture.Store.Requests.Count);
    }

    [Fact]
    public async Task KRX_008113_shaped_build_correction_fits_without_source_duplication()
    {
        const string sourcePath =
            "src/Kronxy.Web/Components/Pages/JobDetail.razor";
        const string testPath =
            "tests/Kronxy.Web.Tests/JobDetailTests.cs";
        string sourcePrefix = string.Join('\n', Enumerable.Range(1, 220)
            .Select(line => $"line {line:D3} \"\\\\" + new string('x', 63))) + "\n";
        string source = sourcePrefix + new string(
            's', 19_181 - Encoding.UTF8.GetByteCount(sourcePrefix));
        string testPrefix = "public sealed class JobDetailTests {}\n";
        string tests = testPrefix + new string('t',
            1_377 - Encoding.UTF8.GetByteCount(testPrefix));
        Assert.Equal(19_181, Encoding.UTF8.GetByteCount(source));
        Assert.Equal(1_377, Encoding.UTF8.GetByteCount(tests));
        Fixture fixture = CreateFixture();
        DeveloperExecutionRequest request = BuildCorrectionRequest();
        string root = request.Repository.RepositoryPath;
        string sourceFullPath = Path.Combine(root, sourcePath);
        string testFullPath = Path.Combine(root, testPath);
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFullPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(testFullPath)!);
        File.WriteAllText(sourceFullPath, source);
        File.WriteAllText(testFullPath, tests);
        ValidatedDeveloperChange[] originalChanges =
        [
            new(DeveloperChangeOperationType.ReplaceFile, sourcePath,
                "Add copy controls.", source, new string('a', 64),
                Encoding.UTF8.GetByteCount(source)),
            new(DeveloperChangeOperationType.CreateFile, testPath,
                "Add focused tests.", tests, string.Empty,
                Encoding.UTF8.GetByteCount(tests))
        ];
        string[] errors =
        [
            $"{sourcePath}(17,40): error CS1503: Argument 1: cannot convert from 'System.Guid' to 'string?'",
            $"{sourcePath}(204,15): error CS0103: The name 'JSRuntime' does not exist in the current context",
            $"{sourcePath}(209,35): error CS1503: Argument 1: cannot convert from 'System.Guid' to 'string?'",
            $"{sourcePath}(210,19): error CS0103: The name 'JSRuntime' does not exist in the current context"
        ];
        string stdout = string.Join('\n',
            Enumerable.Repeat("warning CS8618: inherited warning", 2_200)
                .Concat(errors)
                .Concat(["Build FAILED."]));
        request = request with
        {
            BuildCorrection = request.BuildCorrection! with
            {
                OriginalProposal = request.BuildCorrection.OriginalProposal with
                {
                    Changes = originalChanges
                },
                BuildStandardOutput = stdout
            }
        };
        fixture.Reader.PlanContent = JsonSerializer.SerializeToUtf8Bytes(
            new PlannerPlan
            {
                Objective = "Implement safely.",
                FilesToInspect = [sourcePath],
                CandidateFilesToModify = [sourcePath, testPath],
                Strategy = "Correct build errors.",
                AcceptanceCriteria = ["Build succeeds."],
                Risks = [], ExpectedTests = [], Assumptions = [], Uncertainties = []
            });
        fixture.Gateway.Response = SuccessResponse(
            $$"""
            {"summary":"Fix copy controls.","changes":[{"operation":"ReplaceFile","relativePath":"{{sourcePath}}","intent":"Fix compiler errors.","content":"corrected\n","expectedContentSha256":""}],"assumptions":[],"risks":[]}
            """);

        string rawBefore = request.BuildCorrection.BuildStandardOutput;
        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(request);

        Assert.True(result.IsSuccess, result.ErrorCode);
        Assert.Equal(1, fixture.Gateway.CallCount);
        Assert.Equal(rawBefore, request.BuildCorrection.BuildStandardOutput);
        string input = fixture.Gateway.LastRequest!.UserContent;
        Assert.All(errors, error =>
        {
            string code = error.Contains("CS1503", StringComparison.Ordinal)
                ? "CS1503"
                : "CS0103";
            Assert.Contains(code, input);
        });
        Assert.Contains("\"Line\":17", input);
        Assert.Contains("\"Line\":204", input);
        Assert.Contains("\"Line\":209", input);
        Assert.Contains("\"Line\":210", input);
        Assert.Contains("Build FAILED.", input);
        Assert.DoesNotContain("inherited warning", input);
        Assert.DoesNotContain("EffectiveSource", input);
        const string targetMarker = "AUTHORIZED TARGET FILES:\n";
        int targetStart = input.IndexOf(targetMarker, StringComparison.Ordinal) +
            targetMarker.Length;
        int targetEnd = input.IndexOf(
            "\n\nAUTHORIZED CORRECTION EVIDENCE:", targetStart,
            StringComparison.Ordinal);
        using (JsonDocument targetsDocument = JsonDocument.Parse(
            input[targetStart..targetEnd]))
        {
            JsonElement[] targets = targetsDocument.RootElement
                .EnumerateArray().ToArray();
            Assert.Single(targets);
            Assert.Equal(source, targets.Single(item =>
                item.GetProperty("Path").GetString() == sourcePath)
                .GetProperty("Content").GetString());
        }
        Assert.Contains(sourcePath, input);
        Assert.DoesNotContain(testPath, input);
        int schemaCharacters = DeveloperContractSchema.CreateSchema()
            .GetRawText().Length;
        int estimatedInputTokens = (fixture.Gateway.LastRequest.SystemInstructions.Length +
            input.Length + schemaCharacters + 2) / 3;
        var promptJson = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        int escapedTargetCharacters = JsonSerializer.Serialize(source, promptJson).Length;
        int minimumOutputTokens = Math.Max(
            2_048, (escapedTargetCharacters + 1_024 + 2) / 3);
        Console.WriteLine(
            $"KRX_008113 envelope input={estimatedInputTokens} minimumOutput={minimumOutputTokens} total={estimatedInputTokens + minimumOutputTokens + 512} headroom={16_384 - 512 - minimumOutputTokens - estimatedInputTokens}");
        Assert.InRange(estimatedInputTokens, 1, 8_916);
        Assert.True(minimumOutputTokens >= 6_956);
        Assert.True(fixture.Gateway.LastRequest.Generation!.MaxOutputTokens >=
            minimumOutputTokens);
        Assert.True(16_384 - 512 - minimumOutputTokens - estimatedInputTokens > 0);
        const string evidenceMarker = "AUTHORIZED CORRECTION EVIDENCE:\n";
        int evidenceStart = input.IndexOf(evidenceMarker, StringComparison.Ordinal) +
            evidenceMarker.Length;
        int evidenceEnd = input.IndexOf(
            "\n\nFINAL OUTPUT REQUIREMENTS:", evidenceStart,
            StringComparison.Ordinal);
        const string planMarker = "APPROVED PLANNER PLAN:\n";
        int planStart = input.IndexOf(planMarker, StringComparison.Ordinal) +
            planMarker.Length;
        int planEnd = input.IndexOf("\n\nAUTHORIZED TARGET FILES:", planStart,
            StringComparison.Ordinal);
        Console.WriteLine(
            $"KRX_008113 chars system={fixture.Gateway.LastRequest.SystemInstructions.Length} request={request.JobRequest.Length} planning={planEnd - planStart} targets={targetEnd - targetStart} evidence={evidenceEnd - evidenceStart} outputAndLabels={input.Length - request.JobRequest.Length - (planEnd - planStart) - (targetEnd - targetStart) - (evidenceEnd - evidenceStart)} schema={schemaCharacters}");
        Assert.InRange(evidenceEnd - evidenceStart, 1, 8_188);
    }

    [Fact]
    public async Task Genuinely_impossible_build_correction_envelope_fails_closed()
    {
        DeveloperExecutionRequest request = BuildCorrectionRequest();
        string content = new('x', 50_000);
        File.WriteAllText(
            Path.Combine(request.Repository.RepositoryPath, "src", "new.cs"),
            content);
        request = request with
        {
            BuildCorrection = request.BuildCorrection! with
            {
                OriginalProposal = request.BuildCorrection.OriginalProposal with
                {
                    Changes =
                    [
                        new ValidatedDeveloperChange(
                            DeveloperChangeOperationType.ReplaceFile,
                            "src/new.cs", "Change source.", content,
                            new string('a', 64), 50_000)
                    ]
                }
            }
        };
        Fixture fixture = CreateFixture();
        fixture.Reader.PlanContent = JsonSerializer.SerializeToUtf8Bytes(
            new PlannerPlan
            {
                Objective = "Implement safely.", FilesToInspect = ["src/new.cs"],
                CandidateFilesToModify = ["src/new.cs"], Strategy = "Fix build.",
                AcceptanceCriteria = ["Build succeeds."], Risks = [],
                ExpectedTests = [], Assumptions = [], Uncertainties = []
            });
        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("DEVELOPER_CONTEXT_ENVELOPE_EXCEEDED", result.ErrorCode);
        Assert.Equal(0, fixture.Gateway.CallCount);
    }

    [Fact]
    public async Task Build_correction_selects_all_and_only_diagnostic_authorized_targets()
    {
        const string firstPath = "src/new.cs";
        const string secondPath = "src/second.cs";
        DeveloperExecutionRequest request = BuildCorrectionRequest();
        string root = request.Repository.RepositoryPath;
        const string firstContent = "class NewType {}";
        const string secondContent = "class SecondType {}";
        File.WriteAllText(Path.Combine(root, secondPath), secondContent);
        request = request with
        {
            BuildCorrection = request.BuildCorrection! with
            {
                OriginalProposal = request.BuildCorrection.OriginalProposal with
                {
                    Changes =
                    [
                        new ValidatedDeveloperChange(
                            DeveloperChangeOperationType.CreateFile, firstPath,
                            "Create first.", firstContent, string.Empty,
                            firstContent.Length),
                        new ValidatedDeveloperChange(
                            DeveloperChangeOperationType.CreateFile, secondPath,
                            "Create second.", secondContent, string.Empty,
                            secondContent.Length)
                    ]
                },
                BuildStandardOutput =
                    $"{firstPath}(1,1): error CS0103: Missing first\n" +
                    $"{secondPath}(1,1): error CS0103: Missing second"
            }
        };
        Fixture fixture = CreateFixture();
        fixture.Reader.PlanContent = JsonSerializer.SerializeToUtf8Bytes(
            new PlannerPlan
            {
                Objective = "Implement safely.", FilesToInspect = [firstPath],
                CandidateFilesToModify = [firstPath, secondPath],
                Strategy = "Fix diagnostics.",
                AcceptanceCriteria = ["Build succeeds."], Risks = [],
                ExpectedTests = [], Assumptions = [], Uncertainties = []
            });
        fixture.Gateway.Response = SuccessResponse(
            $$"""
            {"summary":"Fix first target.","changes":[{"operation":"ReplaceFile","relativePath":"{{firstPath}}","intent":"Fix first error.","content":"class NewType { int Value; }","expectedContentSha256":""}],"assumptions":[],"risks":[]}
            """);

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(request);

        Assert.True(result.IsSuccess, result.ErrorCode);
        string input = fixture.Gateway.LastRequest!.UserContent;
        Assert.Contains($"\"Path\":\"{firstPath}\"", input);
        Assert.Contains($"\"Path\":\"{secondPath}\"", input);
        Assert.Contains($"- {firstPath}", input);
        Assert.Contains($"- {secondPath}", input);
    }

    [Fact]
    public async Task Build_correction_rejects_unauthorized_diagnostic_path()
    {
        DeveloperExecutionRequest request = BuildCorrectionRequest();
        request = request with
        {
            BuildCorrection = request.BuildCorrection! with
            {
                BuildStandardOutput =
                    "src/unauthorized.cs(1,1): error CS0103: Missing symbol"
            }
        };
        Fixture fixture = CreateFixture();

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("DEVELOPER_BUILD_DIAGNOSTIC_PATH_NOT_AUTHORIZED",
            result.ErrorCode);
        Assert.Equal(0, fixture.Gateway.CallCount);
    }

    [Fact]
    public async Task Build_correction_rejects_unsafe_diagnostic_path()
    {
        DeveloperExecutionRequest request = BuildCorrectionRequest();
        request = request with
        {
            BuildCorrection = request.BuildCorrection! with
            {
                BuildStandardOutput =
                    "../outside.cs(1,1): error CS0103: Missing symbol"
            }
        };
        Fixture fixture = CreateFixture();

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("DEVELOPER_BUILD_DIAGNOSTIC_UNREPRESENTABLE",
            result.ErrorCode);
        Assert.Equal(0, fixture.Gateway.CallCount);
    }

    [Fact]
    public async Task Originally_created_file_is_replaceable_when_later_diagnostic_associates_it()
    {
        Fixture fixture = CreateFixture();
        fixture.Gateway.Response = SuccessResponse(
            """
            {"summary":"Fix created file.","changes":[{"operation":"ReplaceFile","relativePath":"src/new.cs","intent":"Fix compiler error.","content":"class NewType { int Value; }","expectedContentSha256":""}],"assumptions":[],"risks":[]}
            """);

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(
            BuildCorrectionRequest());

        Assert.True(result.IsSuccess, result.ErrorCode);
        ValidatedDeveloperChange change = Assert.Single(result.Proposal!.Changes);
        Assert.Equal(DeveloperChangeOperationType.ReplaceFile, change.Operation);
        Assert.Equal("src/new.cs", change.RelativePath);
        Assert.Contains("\"Path\":\"src/new.cs\"",
            fixture.Gateway.LastRequest!.UserContent);
    }

    [Fact]
    public async Task Oversized_warning_only_build_output_fails_closed()
    {
        DeveloperExecutionRequest request = BuildCorrectionRequest();
        DeveloperBuildCorrectionContext correction = request.BuildCorrection! with
        {
            BuildStandardOutput = string.Join('\n',
                Enumerable.Repeat("warning CS8618: inherited warning", 1_000))
        };

        BuildCorrectionEvidenceResult result =
            await BuildCorrectionEvidenceCompactor.CreateAsync(
                correction, request.Repository, 8_188);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "DEVELOPER_BUILD_CORRECTION_EVIDENCE_TOO_LARGE",
            result.ErrorCode);
    }

    [Fact]
    public async Task Build_correction_evidence_is_deterministic_and_within_feedback_budget()
    {
        DeveloperExecutionRequest request = BuildCorrectionRequest();
        string error = "src/new.cs(8,24): error CS0050: Inconsistent accessibility";
        DeveloperBuildCorrectionContext correction = request.BuildCorrection! with
        {
            BuildStandardOutput = error + "\n" + error
        };
        BuildCorrectionEvidenceResult first =
            await BuildCorrectionEvidenceCompactor.CreateAsync(
                correction, request.Repository, 8_188);
        BuildCorrectionEvidenceResult second =
            await BuildCorrectionEvidenceCompactor.CreateAsync(
                correction, request.Repository, 8_188);

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Content, second.Content);
        Assert.True(first.Content.Length <= 8_188);
        Assert.Equal(1, first.UniqueCount);
        Assert.Equal(1, first.DuplicateCount);
        Assert.Contains("\"Path\":\"src/new.cs\"", first.Content);
        Assert.Contains("\"Line\":8", first.Content);
        Assert.Contains("\"Column\":24", first.Content);
    }

    [Fact]
    public async Task Job02b_sized_build_evidence_preserves_both_unique_errors()
    {
        DeveloperExecutionRequest request = BuildCorrectionRequest();
        string root = request.Repository.RepositoryPath;
        string[] paths =
        [
            "src/ProjectAreas/ProjectArea.cs",
            "src/ProjectAreas/ProjectAreaErrors.cs",
            "src/ProjectAreas/IProjectAreaRepository.cs"
        ];
        string[] contents =
        [
            "class ProjectArea : Entity\n" + new string('a', 1_470),
            "class ProjectAreaErrors {}\n" + new string('b', 870),
            "interface IProjectAreaRepository {}\n" + new string('c', 240)
        ];
        Directory.CreateDirectory(Path.Combine(root, "src", "ProjectAreas"));
        for (int index = 0; index < paths.Length; index++)
            File.WriteAllText(Path.Combine(root, paths[index]), contents[index]);
        const string referencePath = "src/ProjectModules/ProjectModule.cs";
        Directory.CreateDirectory(Path.Combine(root, "src", "ProjectModules"));
        File.WriteAllText(
            Path.Combine(root, referencePath),
            "public sealed class ProjectModule : Entity\n");
        ValidatedDeveloperChange[] changes = paths.Select((path, index) =>
            new ValidatedDeveloperChange(
                DeveloperChangeOperationType.CreateFile, path, "Create file.",
                contents[index], string.Empty, contents[index].Length)).ToArray();
        string absoluteRepositoryPath = Path.Combine(root, paths[2]);
        string error0 =
            absoluteRepositoryPath + "(8,24): error CS0050: Inconsistent accessibility: return type 'Task<ProjectArea?>' is less accessible than method 'GetByIdAsync' [" + root + "/Kronxy.Domain.csproj]";
        string error1 =
            absoluteRepositoryPath + "(9,10): error CS0051: Inconsistent accessibility: parameter type 'ProjectArea' is less accessible than method 'Add' [" + root + "/Kronxy.Domain.csproj]";
        string stdout = string.Join('\n',
            Enumerable.Repeat("warning CS0108: irrelevant", 210)
                .Concat([error0, error1, error0, error1]));
        DeveloperBuildCorrectionContext correction = request.BuildCorrection! with
        {
            OriginalProposal = request.BuildCorrection.OriginalProposal with
            {
                Changes = changes
            },
            BuildStandardOutput = stdout
        };

        BuildCorrectionEvidenceResult compacted =
            await BuildCorrectionEvidenceCompactor.CreateAsync(
                correction, request.Repository, 8_188, paths, [referencePath]);

        Assert.True(compacted.IsSuccess);
        Assert.Equal(2, compacted.UniqueCount);
        Assert.Equal(2, compacted.DuplicateCount);
        Assert.Equal(2, compacted.IncludedCount);
        Assert.True(compacted.Content.Length < 8_188);
        Assert.Contains("CS0050", compacted.Content);
        Assert.Contains("CS0051", compacted.Content);
        Assert.Contains("\"Path\":\"src/ProjectAreas/IProjectAreaRepository.cs\"", compacted.Content);
        Assert.DoesNotContain(root, compacted.Content);
        Assert.DoesNotContain("irrelevant", compacted.Content);
        using (JsonDocument document = JsonDocument.Parse(compacted.Content))
        {
            JsonElement effectiveSource = Assert.Single(
                document.RootElement.GetProperty("EffectiveSource")
                    .EnumerateArray()
                    .Where(item => item.GetProperty("Path").GetString() == paths[0]));
            Assert.Equal(contents[0], effectiveSource.GetProperty("Content").GetString());
        }
        Assert.Contains("ProjectAreaErrors.cs", compacted.Content);
        Assert.Contains("\"RelatedSymbols\":[\"ProjectArea\"]", compacted.Content);
        Assert.Contains("\"Symbol\":\"ProjectArea\"", compacted.Content);
        Assert.Contains("\"Status\":\"RESOLVED\"", compacted.Content);
        Assert.Contains("\"Path\":\"src/ProjectAreas/ProjectArea.cs\"", compacted.Content);
        Assert.Contains("\"Signature\":\"class ProjectArea : Entity\"", compacted.Content);
        Assert.Contains("\"CanModify\":true", compacted.Content);
        Assert.Contains("\"Path\":\"src/ProjectModules/ProjectModule.cs\"", compacted.Content);
        Assert.Contains("\"Signature\":\"public sealed class ProjectModule : Entity\"", compacted.Content);
        Assert.True(compacted.RelatedDeclarationsCharacters > 0);
    }

    [Fact]
    public async Task Build_correction_ambiguous_related_declaration_does_not_guess_target()
    {
        DeveloperExecutionRequest request = BuildCorrectionRequest();
        string root = request.Repository.RepositoryPath;
        string[] paths = ["src/first.cs", "src/second.cs"];
        foreach (string path in paths)
            File.WriteAllText(Path.Combine(root, path), "internal class Duplicate {}\n");
        ValidatedDeveloperChange[] changes = paths.Select(path =>
            new ValidatedDeveloperChange(
                DeveloperChangeOperationType.CreateFile, path, "Create type.",
                "internal class Duplicate {}\n", string.Empty, 28)).ToArray();
        DeveloperBuildCorrectionContext correction = request.BuildCorrection! with
        {
            OriginalProposal = request.BuildCorrection.OriginalProposal with { Changes = changes },
            BuildStandardOutput =
                "src/first.cs(1,1): error CS9999: Symbol 'Duplicate' is inconsistent"
        };

        BuildCorrectionEvidenceResult result =
            await BuildCorrectionEvidenceCompactor.CreateAsync(
                correction, request.Repository, 8_188, paths, []);

        Assert.True(result.IsSuccess);
        using JsonDocument document = JsonDocument.Parse(result.Content);
        JsonElement related = Assert.Single(
            document.RootElement.GetProperty("RelatedDeclarations").EnumerateArray());
        Assert.Equal("Duplicate", related.GetProperty("Symbol").GetString());
        Assert.Equal("AMBIGUOUS", related.GetProperty("Status").GetString());
        Assert.Equal(JsonValueKind.Null, related.GetProperty("Declaration").ValueKind);
        Assert.Equal(JsonValueKind.Null, related.GetProperty("ReferencePattern").ValueKind);
    }

    [Fact]
    public async Task Build_correction_declaration_outside_planner_boundary_is_not_exposed()
    {
        DeveloperExecutionRequest request = BuildCorrectionRequest();
        string root = request.Repository.RepositoryPath;
        File.WriteAllText(Path.Combine(root, "src", "outside.cs"), "public class Hidden {}\n");
        DeveloperBuildCorrectionContext correction = request.BuildCorrection! with
        {
            BuildStandardOutput =
                "src/new.cs(1,1): error CS9999: Symbol 'Hidden' is inconsistent"
        };

        BuildCorrectionEvidenceResult result =
            await BuildCorrectionEvidenceCompactor.CreateAsync(
                correction, request.Repository, 8_188, ["src/new.cs"], []);

        Assert.True(result.IsSuccess);
        using JsonDocument document = JsonDocument.Parse(result.Content);
        Assert.Empty(document.RootElement.GetProperty("RelatedDeclarations").EnumerateArray());
        Assert.DoesNotContain("src/outside.cs", result.Content);
    }

    [Fact]
    public async Task Build_correction_unique_error_too_large_fails_closed()
    {
        Fixture fixture = CreateFixture();
        DeveloperExecutionRequest request = BuildCorrectionRequest();
        request = request with
        {
            BuildCorrection = request.BuildCorrection! with
            {
                BuildStandardOutput =
                    "src/new.cs(1,1): error CS0050: " + new string('x', 9_000)
            }
        };

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(request);

        Assert.Equal(DeveloperExecutionFailureKind.ContextTooLarge, result.FailureKind);
        Assert.Equal("DEVELOPER_BUILD_CORRECTION_EVIDENCE_TOO_LARGE", result.ErrorCode);
        Assert.Equal(0, fixture.Gateway.CallCount);
    }

    [Fact]
    public async Task Build_correction_source_mismatch_fails_before_ai()
    {
        Fixture fixture = CreateFixture();
        DeveloperExecutionRequest request = BuildCorrectionRequest();
        File.WriteAllText(
            Path.Combine(request.Repository.RepositoryPath, "src", "new.cs"),
            "class Different {}");

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(request);

        Assert.Equal(
            "DEVELOPER_BUILD_CORRECTION_SOURCE_MISMATCH",
            result.ErrorCode);
        Assert.Equal(0, fixture.Gateway.CallCount);
    }

    [Fact]
    public async Task Build_correction_no_op_is_rejected_before_proposal_persistence()
    {
        Fixture fixture = CreateFixture();
        fixture.MetadataBinder.ReplaceHash = CurrentHash("class NewType {}");
        fixture.Gateway.Response = SuccessResponse(
            """
            {"summary":"No effective correction.","changes":[{"operation":"ReplaceFile","relativePath":"src/new.cs","intent":"No change.","content":"class NewType {}","expectedContentSha256":"ignored"}],"assumptions":[],"risks":[]}
            """);

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(
            BuildCorrectionRequest());

        Assert.Equal(DeveloperExecutionFailureKind.PolicyRejected, result.FailureKind);
        Assert.Equal("DEVELOPER_BUILD_CORRECTION_NO_OP", result.ErrorCode);
        AssertRejectedArtifact(
            fixture,
            ArtifactType.DeveloperBuildCorrectionRejectedResponse,
            "DEVELOPER_BUILD_CORRECTION_NO_OP");
        Assert.DoesNotContain(fixture.Store.Requests,
            item => item.ArtifactType == ArtifactType.DeveloperBuildCorrectionProposal);
    }

    [Fact]
    public async Task Build_correction_uses_quality_model_without_changing_job_identity()
    {
        Fixture fixture = CreateFixture();
        fixture.Gateway.Response = SuccessResponse(
            """
            {"summary":"Change accessibility.","changes":[{"operation":"ReplaceFile","relativePath":"src/new.cs","intent":"Expose the type.","content":"public class NewType {}","expectedContentSha256":"ignored"}],"assumptions":[],"risks":[]}
            """);
        DeveloperExecutionRequest request = BuildCorrectionRequest();

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(AiLogicalModel.CodingQuality,
            fixture.Gateway.LastRequest!.Model);
        Assert.Equal(request.JobId, result.Report!.JobId);
        Assert.Equal(request.RunId, result.Report.RunId);
    }

    [Fact]
    public async Task Build_correction_retry_includes_no_op_feedback_and_uses_versioned_artifacts()
    {
        Fixture fixture = CreateFixture();
        fixture.Gateway.Response = SuccessResponse(
            """
            {"summary":"Change accessibility.","changes":[{"operation":"ReplaceFile","relativePath":"src/new.cs","intent":"Expose the type.","content":"public class NewType {}","expectedContentSha256":"ignored"}],"assumptions":[],"risks":[]}
            """);
        DeveloperExecutionRequest request = BuildCorrectionRequest();
        request = request with
        {
            BuildCorrection = request.BuildCorrection! with
            {
                PreviousNoOpPaths = ["src/new.cs"],
                BuildStandardOutput = "src/new.cs(1,1): error CS0050: Inconsistent accessibility"
            }
        };

        DeveloperExecutionResult result = await fixture.Service.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Contains("src/new.cs", fixture.Gateway.LastRequest!.UserContent);
        Assert.Contains("CS0050", fixture.Gateway.LastRequest.UserContent);
        Assert.Contains("no-op", fixture.Gateway.LastRequest.SystemInstructions,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(fixture.Store.Requests,
            item => item.ArtifactType == ArtifactType.DeveloperBuildCorrectionRetryProposal);
        Assert.Contains(fixture.Store.Requests,
            item => item.ArtifactType == ArtifactType.DeveloperBuildCorrectionRetryResponse);
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
        string repositoryPath = Path.Combine(
            Path.GetTempPath(), "kronxy-build-correction-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(repositoryPath, "src"));
        File.WriteAllText(Path.Combine(repositoryPath, "src", "new.cs"), "class NewType {}");
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
            Repository = request.Repository with { RepositoryPath = repositoryPath },
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

    private static byte[] ContextPackage(
        params (string Path, string Content)[] files)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(
            stream,
            ZipArchiveMode.Create,
            leaveOpen: true))
        {
            foreach ((string path, string content) in files)
            {
                ZipArchiveEntry source = archive.CreateEntry(path);
                using var writer = new StreamWriter(
                    source.Open(),
                    new UTF8Encoding(false),
                    bufferSize: 1024,
                    leaveOpen: false);
                writer.Write(content);
            }

            ZipArchiveEntry manifest = archive.CreateEntry("manifest.json");
            using Stream output = manifest.Open();
            JsonSerializer.Serialize(output, new
            {
                entries = files.Select(file => new
                {
                    path = file.Path,
                    sizeBytes = Encoding.UTF8.GetByteCount(file.Content),
                    sha256 = CurrentHash(file.Content)
                })
            });
        }

        return stream.ToArray();
    }

    private static Fixture CreateFixture(
        int developerContextCharacters = 48_000,
        int developerContextWindowTokens = 16_384)
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
            DeveloperContextWindowTokens = developerContextWindowTokens,
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
                            ? File.Exists(Path.Combine(
                                request.Repository.RepositoryPath,
                                change.RelativePath))
                                ? CurrentHash(File.ReadAllText(Path.Combine(
                                    request.Repository.RepositoryPath,
                                    change.RelativePath)))
                                : ReplaceHash
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
        public byte[] ContextContent { get; set; } = [1, 2, 3];

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
                : ContextContent;
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

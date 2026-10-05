using System.Security.Cryptography;
using System.Text;
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

        Assert.True(result.IsSuccess);
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

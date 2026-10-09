using System.Text;
using Kronxy.Web.Models;
using Kronxy.Web.Presentation;
using Xunit;

namespace Kronxy.Web.Tests;

public sealed class ArtifactDiagnosticsTests
{
    [Theory]
    [InlineData("PlanningRejectedResponse")]
    [InlineData("BuildFailureReport")]
    [InlineData("ValidationError")]
    [InlineData("RecoveryDiagnostic")]
    [InlineData("HumanReviewCorrection")]
    public void Diagnostic_matcher_supports_generic_artifact_patterns(string type) =>
        Assert.True(ArtifactDiagnostics.IsDiagnostic(type));

    [Theory]
    [InlineData("PlanningPlan")]
    [InlineData("AiResponse")]
    [InlineData("BuildReport")]
    [InlineData("GovernedHumanCorrectionReceipt")]
    public void Diagnostic_matcher_does_not_mark_normal_success_artifacts(string type) =>
        Assert.False(ArtifactDiagnostics.IsDiagnostic(type));

    [Fact]
    public void Matching_correlation_is_preferred_over_newer_unrelated_diagnostic()
    {
        ArtifactDto matching = Artifact("PlanningRejectedResponse", "expected", 1);
        ArtifactDto newer = Artifact("BuildFailure", "other", 2);

        Assert.Same(
            matching,
            ArtifactDiagnostics.Select([matching, newer], "expected"));
    }

    [Fact]
    public void Latest_same_run_diagnostic_is_fallback_without_exact_correlation()
    {
        ArtifactDto older = Artifact("PlanningRejectedResponse", "first", 1);
        ArtifactDto newer = Artifact("ValidationError", "second", 2);

        Assert.Same(
            newer,
            ArtifactDiagnostics.Select([older, newer], "missing"));
    }

    [Fact]
    public void No_diagnostic_candidate_returns_null()
    {
        Assert.Null(ArtifactDiagnostics.Select(
            [Artifact("PlanningPlan", "plan", 1)],
            "plan"));
    }

    [Fact]
    public void Planning_rejection_formats_nested_json_and_surfaces_metadata_and_paths()
    {
        const string content = """
            {
              "Status": 0,
              "Content": "{\"objective\":\"Improve diagnostics\",\"filesToInspect\":[\"src/Kronxy.Web/Components/Pages/JobDetail.razor\"],\"candidateFilesToModify\":[\"src/Kronxy.Web/Views/JobDetail.cshtml\"]}",
              "Provider": "Ollama",
              "LogicalModel": "CodingQuality",
              "PhysicalModel": "qwen2.5-coder:14b",
              "Duration": "00:01:24.7791563",
              "TerminationReason": 10,
              "Usage": { "PromptTokens": 3238, "CompletionTokens": 530 },
              "ErrorCode": "",
              "IsSuccess": true
            }
            """;

        ArtifactDiagnosticPresentation view = ArtifactDiagnostics.Present(Json(content));

        Assert.True(view.NestedContentIsJson);
        Assert.Contains("\"objective\": \"Improve diagnostics\"", view.NestedContent);
        Assert.DoesNotContain("\\\"objective\\\"", view.NestedContent);
        Assert.Equal("Ollama", view.Provider);
        Assert.Equal("CodingQuality", view.LogicalModel);
        Assert.Equal("qwen2.5-coder:14b", view.PhysicalModel);
        Assert.Equal("00:01:24.7791563", view.Duration);
        Assert.Equal("3238", view.PromptTokens);
        Assert.Equal("530", view.CompletionTokens);
        Assert.Equal("10", view.TerminationReason);
        Assert.Equal("Success", view.SuccessStatus);
        Assert.Null(view.ErrorCode);
        Assert.Equal(
            "src/Kronxy.Web/Components/Pages/JobDetail.razor",
            Assert.Single(view.FilesToInspect));
        Assert.Equal(
            "src/Kronxy.Web/Views/JobDetail.cshtml",
            Assert.Single(view.CandidateFilesToModify));
    }

    [Fact]
    public void Invalid_nested_json_falls_back_to_plain_provider_content()
    {
        ArtifactDiagnosticPresentation view = ArtifactDiagnostics.Present(
            Json("""{"Content":"not json","Provider":"Ollama"}"""));

        Assert.False(view.NestedContentIsJson);
        Assert.Equal("not json", view.NestedContent);
    }

    [Fact]
    public void Normal_json_remains_formatted_without_diagnostic_metadata()
    {
        ArtifactDiagnosticPresentation view = ArtifactDiagnostics.Present(
            Json("""{"result":{"ok":true}}"""));

        Assert.Contains("\"ok\": true", view.RawContent);
        Assert.Null(view.NestedContent);
        Assert.Null(view.Provider);
    }

    [Fact]
    public void Plain_text_remains_unchanged()
    {
        var artifact = new ArtifactContentDto(
            Encoding.UTF8.GetBytes("plain diagnostic"),
            "text/plain",
            null);

        Assert.Equal(
            "plain diagnostic",
            ArtifactDiagnostics.Present(artifact).RawContent);
    }

    [Fact]
    public void Binary_artifact_behavior_remains_non_text()
    {
        var artifact = new ArtifactContentDto(
            [0, 1, 2],
            "application/octet-stream",
            "evidence.bin");

        Assert.False(artifact.IsText);
    }

    private static ArtifactContentDto Json(string content) =>
        new(
            Encoding.UTF8.GetBytes(content),
            "application/json",
            null);

    private static ArtifactDto Artifact(
        string type,
        string correlationId,
        int minute) =>
        new(
            Guid.NewGuid(),
            type,
            $"/api/jobs/a/artifacts/{Guid.NewGuid():N}",
            new string('a', 64),
            new DateTimeOffset(2026, 1, 1, 0, minute, 0, TimeSpan.Zero),
            correlationId,
            100);
}

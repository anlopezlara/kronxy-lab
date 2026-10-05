using System.Text.Json;
using Kronxy.Application.Execution;
using Kronxy.Domain.Jobs;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class GovernedHumanCorrectionPolicyTests
{
    private const string Path = "src/Kronxy.Domain/ProjectAreas/ProjectArea.cs";
    private const string OtherPath = "src/Kronxy.Domain/ProjectAreas/ProjectAreaErrors.cs";
    private const string CurrentSha =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void Exhausted_build_correction_is_eligible_for_human_correction()
    {
        Assert.True(GovernedHumanCorrectionPolicy.IsEligible(
            JobState.Building,
            isTerminal: false,
            hasValidFailureEvidence: true,
            aiCorrectionExhausted: true,
            [Path]));
    }

    [Theory]
    [InlineData(JobState.Planning, false, true, true)]
    [InlineData(JobState.Building, true, true, true)]
    [InlineData(JobState.Building, false, false, true)]
    [InlineData(JobState.Building, false, true, false)]
    public void Ineligible_state_or_evidence_fails_closed(
        JobState state,
        bool terminal,
        bool hasFailure,
        bool exhausted)
    {
        Assert.False(GovernedHumanCorrectionPolicy.IsEligible(
            state, terminal, hasFailure, exhausted, [Path]));
    }

    [Fact]
    public void Request_inside_planner_allowlist_is_valid()
    {
        Assert.True(GovernedHumanCorrectionPolicy.IsValidRequest(
            Request(Change(Path, "public class ProjectArea {}")),
            [Path, OtherPath]));
    }

    [Theory]
    [InlineData("src/Kronxy.Api/Program.cs")]
    [InlineData("../ProjectArea.cs")]
    [InlineData("src//ProjectArea.cs")]
    public void Outside_or_unsafe_path_fails_closed(string path)
    {
        Assert.False(GovernedHumanCorrectionPolicy.IsValidRequest(
            Request(Change(path, "changed")),
            [Path, OtherPath]));
    }

    [Fact]
    public void Stale_expected_sha_fails_current_source_match()
    {
        Assert.False(GovernedHumanCorrectionPolicy.MatchesCurrentSource(
            Change(Path, "changed"),
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"));
    }

    [Fact]
    public void Matching_expected_sha_passes_current_source_match()
    {
        Assert.True(GovernedHumanCorrectionPolicy.MatchesCurrentSource(
            Change(Path, "changed"), CurrentSha));
    }

    [Fact]
    public void Replacement_with_same_hash_is_rejected_as_no_op()
    {
        const string content = "unchanged";
        string hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(content)))
            .ToLowerInvariant();

        Assert.True(GovernedHumanCorrectionPolicy.IsNoOp(
            Change(Path, content) with { ExpectedContentSha256 = hash }));
    }

    [Fact]
    public void Changed_replacement_is_not_a_no_op()
    {
        Assert.False(GovernedHumanCorrectionPolicy.IsNoOp(
            Change(Path, "changed")));
    }

    [Fact]
    public void Same_correlation_and_content_has_deterministic_fingerprint()
    {
        GovernedHumanCorrectionRequest first = Request(
            Change(Path, "one"),
            Change(OtherPath, "two"));
        GovernedHumanCorrectionRequest reordered = Request(
            Change(OtherPath, "two"),
            Change(Path, "one"));

        Assert.Equal(
            GovernedHumanCorrectionPolicy.Fingerprint(first),
            GovernedHumanCorrectionPolicy.Fingerprint(reordered));
    }

    [Fact]
    public void Same_correlation_with_different_content_has_different_fingerprint()
    {
        Assert.NotEqual(
            GovernedHumanCorrectionPolicy.Fingerprint(
                Request(Change(Path, "one"))),
            GovernedHumanCorrectionPolicy.Fingerprint(
                Request(Change(Path, "two"))));
    }

    [Fact]
    public void Evidence_round_trip_preserves_job_run_and_attempt()
    {
        Guid jobId = Guid.NewGuid();
        Guid runId = Guid.NewGuid();
        var evidence = new GovernedHumanCorrectionEvidence
        {
            JobId = jobId,
            RunId = runId,
            AttemptCount = 1,
            Stage = JobState.Building.ToString(),
            Actor = "human",
            CorrelationId = "correlation",
            Reason = "AI corrections exhausted.",
            RequestSha256 = CurrentSha,
            FailureEvidence = ["build/build.json"],
            ExhaustedAiCorrectionEvidence = ["developer/retry-rejected.json"],
            AllowedPaths = [Path],
            Proposal = new ValidatedDeveloperProposal(
                "summary",
                [new(
                    DeveloperChangeOperationType.ReplaceFile,
                    Path,
                    "intent",
                    "changed",
                    CurrentSha,
                    7)],
                [],
                [],
                7,
                100),
            RecordedAtUtc = DateTimeOffset.UtcNow
        };

        GovernedHumanCorrectionEvidence? restored =
            JsonSerializer.Deserialize<GovernedHumanCorrectionEvidence>(
                JsonSerializer.SerializeToUtf8Bytes(evidence));

        Assert.NotNull(restored);
        Assert.Equal(jobId, restored.JobId);
        Assert.Equal(runId, restored.RunId);
        Assert.Equal(1, restored.AttemptCount);
    }

    private static GovernedHumanCorrectionRequest Request(
        params GovernedHumanFileReplacement[] changes) =>
        new()
        {
            Actor = "human-review",
            CorrelationId = "governed-human-correction",
            Reason = "AI corrections exhausted.",
            Changes = changes
        };

    private static GovernedHumanFileReplacement Change(
        string path,
        string content) =>
        new()
        {
            RelativePath = path,
            ExpectedContentSha256 = CurrentSha,
            Content = content
        };
}

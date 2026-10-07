using System.Text.Json;
using Kronxy.Application.Artifacts;
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
            rejectedAiCorrectionExhausted: true,
            failedCorrectedRebuildExhausted: false,
            [Path]));
    }

    [Theory]
    [InlineData(JobState.Planning, false, true, true, false)]
    [InlineData(JobState.Building, true, true, true, false)]
    [InlineData(JobState.Building, false, false, true, false)]
    [InlineData(JobState.Building, false, true, false, false)]
    public void Ineligible_state_or_evidence_fails_closed(
        JobState state,
        bool terminal,
        bool hasFailure,
        bool rejected,
        bool failedRebuild)
    {
        Assert.False(GovernedHumanCorrectionPolicy.IsEligible(
            state, terminal, hasFailure, rejected, failedRebuild, [Path]));
    }

    [Fact]
    public void Failed_corrected_rebuild_is_eligible_for_human_correction()
    {
        Assert.True(GovernedHumanCorrectionPolicy.IsEligible(
            JobState.Building,
            isTerminal: false,
            hasValidFailureEvidence: true,
            rejectedAiCorrectionExhausted: false,
            failedCorrectedRebuildExhausted: true,
            [Path]));
    }

    [Fact]
    public void Failed_build_after_applied_human_correction_allows_next_correction()
    {
        Assert.True(GovernedHumanCorrectionPolicy
            .IsSequentialCorrectionEligible(
                JobState.Building,
                isTerminal: false,
                latestCorrectionApplied: true,
                latestObservedChangesValid: true,
                latestBuildFailed: true));
    }

    [Theory]
    [InlineData(JobState.Testing, false, true, true, true)]
    [InlineData(JobState.Building, true, true, true, true)]
    [InlineData(JobState.Building, false, false, true, true)]
    [InlineData(JobState.Building, false, true, false, true)]
    [InlineData(JobState.Building, false, true, true, false)]
    public void Incomplete_or_successful_human_correction_lineage_fails_closed(
        JobState state,
        bool terminal,
        bool applied,
        bool observed,
        bool buildFailed)
    {
        Assert.False(GovernedHumanCorrectionPolicy
            .IsSequentialCorrectionEligible(
                state,
                terminal,
                applied,
                observed,
                buildFailed));
    }

    [Fact]
    public void Applied_observed_correction_with_failed_rebuild_is_exhausted()
    {
        (ValidatedDeveloperProposal proposal, ObservedChangeManifest observed,
            BuildExecutionReport build) = FailedCorrectedRebuild();

        Assert.True(GovernedHumanCorrectionPolicy
            .IsFailedCorrectedRebuildExhaustion(proposal, observed, build));
    }

    [Fact]
    public void Successful_corrected_rebuild_is_not_exhausted()
    {
        (ValidatedDeveloperProposal proposal, ObservedChangeManifest observed,
            BuildExecutionReport build) = FailedCorrectedRebuild();

        Assert.False(GovernedHumanCorrectionPolicy
            .IsFailedCorrectedRebuildExhaustion(
                proposal,
                observed,
                build with
                {
                    Outcome = ToolExecutionOutcome.Completed,
                    ExitCode = 0
                }));
    }

    [Fact]
    public void Missing_or_mismatched_observed_change_is_not_exhausted()
    {
        (ValidatedDeveloperProposal proposal, ObservedChangeManifest observed,
            BuildExecutionReport build) = FailedCorrectedRebuild();

        Assert.False(GovernedHumanCorrectionPolicy
            .IsFailedCorrectedRebuildExhaustion(proposal, null, build));
        Assert.False(GovernedHumanCorrectionPolicy
            .IsFailedCorrectedRebuildExhaustion(
                proposal,
                observed with { RunId = Guid.NewGuid() },
                build));
        Assert.False(GovernedHumanCorrectionPolicy
            .IsFailedCorrectedRebuildExhaustion(
                proposal,
                observed with { Entries = [] },
                build));
    }

    [Fact]
    public void No_op_correction_is_not_failed_rebuild_exhaustion()
    {
        (ValidatedDeveloperProposal proposal, ObservedChangeManifest observed,
            BuildExecutionReport build) = FailedCorrectedRebuild();
        ValidatedDeveloperChange change = proposal.Changes.Single();
        ValidatedDeveloperProposal noOp = proposal with
        {
            Changes = [change with
            {
                ExpectedContentSha256 = Sha(change.Content)
            }]
        };

        Assert.False(GovernedHumanCorrectionPolicy
            .IsFailedCorrectedRebuildExhaustion(noOp, observed, build));
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
            AutomaticCorrectionExhaustionEvidence = [],
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

    [Theory]
    [InlineData(ArtifactType.TestGovernedHumanCorrectionReport)]
    [InlineData(ArtifactType.TestReport)]
    public void Test_stage_correlation_does_not_replace_effective_source_lineage(
        ArtifactType testType)
    {
        Assert.True(GovernedHumanCorrectionPolicy.IsTestBoundToLatestCorrection(
            EffectiveLineageArtifacts(testType),
            "source-2",
            "test-stage",
            testType));
    }

    [Fact]
    public void Sequential_correction_supersedes_prior_correction()
    {
        List<ArtifactRecord> artifacts = EffectiveLineageArtifacts(
            ArtifactType.TestGovernedHumanCorrectionReport).ToList();
        artifacts.Insert(0, Artifact(
            ArtifactType.GovernedHumanCorrectionRequest, "source-1", 0));

        Assert.True(GovernedHumanCorrectionPolicy.IsTestBoundToLatestCorrection(
            artifacts, "source-2", "test-stage",
            ArtifactType.TestGovernedHumanCorrectionReport));
    }

    [Fact]
    public void Newer_correction_after_tests_fails_closed()
    {
        List<ArtifactRecord> artifacts = EffectiveLineageArtifacts(
            ArtifactType.TestGovernedHumanCorrectionReport).ToList();
        artifacts.Add(Artifact(
            ArtifactType.GovernedHumanCorrectionRequest, "source-3", 5));

        Assert.False(GovernedHumanCorrectionPolicy.IsTestBoundToLatestCorrection(
            artifacts, "source-2", "test-stage",
            ArtifactType.TestGovernedHumanCorrectionReport));
    }

    [Fact]
    public void Test_from_stale_stage_correlation_fails_closed()
    {
        Assert.False(GovernedHumanCorrectionPolicy.IsTestBoundToLatestCorrection(
            EffectiveLineageArtifacts(
                ArtifactType.TestGovernedHumanCorrectionReport),
            "source-2", "other-test-stage",
            ArtifactType.TestGovernedHumanCorrectionReport));
    }

    [Fact]
    public void Test_before_effective_build_fails_closed()
    {
        List<ArtifactRecord> artifacts = EffectiveLineageArtifacts(
            ArtifactType.TestGovernedHumanCorrectionReport).ToList();
        artifacts.RemoveAll(artifact => artifact.ArtifactType ==
            ArtifactType.TestGovernedHumanCorrectionReport);
        artifacts.Add(Artifact(
            ArtifactType.TestGovernedHumanCorrectionReport, "test-stage", 2));

        Assert.False(GovernedHumanCorrectionPolicy.IsTestBoundToLatestCorrection(
            artifacts, "source-2", "test-stage",
            ArtifactType.TestGovernedHumanCorrectionReport));
    }

    [Fact]
    public void Build_from_stale_source_lineage_fails_closed()
    {
        List<ArtifactRecord> artifacts = EffectiveLineageArtifacts(
            ArtifactType.TestGovernedHumanCorrectionReport).ToList();
        artifacts.RemoveAll(artifact => artifact.ArtifactType ==
            ArtifactType.BuildGovernedHumanCorrectionReport);
        artifacts.Add(Artifact(
            ArtifactType.BuildGovernedHumanCorrectionReport, "source-1", 3));

        Assert.False(GovernedHumanCorrectionPolicy.IsTestBoundToLatestCorrection(
            artifacts, "source-2", "test-stage",
            ArtifactType.TestGovernedHumanCorrectionReport));
    }

    [Fact]
    public void Missing_observed_source_lineage_fails_closed()
    {
        ArtifactRecord[] artifacts = EffectiveLineageArtifacts(
                ArtifactType.TestGovernedHumanCorrectionReport)
            .Where(artifact => artifact.ArtifactType !=
                ArtifactType.ObservedGovernedHumanCorrectionManifest)
            .ToArray();

        Assert.False(GovernedHumanCorrectionPolicy.IsTestBoundToLatestCorrection(
            artifacts, "source-2", "test-stage",
            ArtifactType.TestGovernedHumanCorrectionReport));
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

    private static IReadOnlyList<ArtifactRecord> EffectiveLineageArtifacts(
        ArtifactType testType) =>
        [
            Artifact(ArtifactType.GovernedHumanCorrectionRequest,
                "source-2", 1),
            Artifact(ArtifactType.ObservedGovernedHumanCorrectionManifest,
                "source-2", 2),
            Artifact(ArtifactType.BuildGovernedHumanCorrectionReport,
                "source-2", 3),
            Artifact(testType, "test-stage", 4)
        ];

    private static ArtifactRecord Artifact(
        ArtifactType type,
        string correlationId,
        int seconds) =>
        new()
        {
            ArtifactId = Guid.NewGuid(),
            JobId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            RunId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ArtifactType = type,
            RelativePath = $"test/{type}-{seconds}",
            Sha256 = new string('a', 64),
            SizeBytes = 1,
            CreatedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(seconds),
            CorrelationId = correlationId
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

    private static (
        ValidatedDeveloperProposal Proposal,
        ObservedChangeManifest Observed,
        BuildExecutionReport Build) FailedCorrectedRebuild()
    {
        Guid jobId = Guid.NewGuid();
        Guid runId = Guid.NewGuid();
        const string content = "public interface IProjectAreaRepository {}";
        var proposal = new ValidatedDeveloperProposal(
            "Correct compilation.",
            [new(
                DeveloperChangeOperationType.ReplaceFile,
                Path,
                "Remove an invalid abstraction.",
                content,
                CurrentSha,
                7)],
            [],
            [],
            7,
            100);
        var observed = new ObservedChangeManifest(
            jobId,
            runId,
            "head",
            [new(
                Path,
                Kronxy.Application.Repositories.ObservedRepositoryChangeKind.Modified,
                Sha(content),
                content.Length)]);
        var build = new BuildExecutionReport(
            jobId,
            runId,
            "Kronxy.sln",
            ToolExecutionOutcome.NonZeroExitCode,
            1,
            "TOOL_NONZERO_EXIT",
            DateTime.UtcNow,
            DateTime.UtcNow,
            TimeSpan.Zero);
        return (proposal, observed, build);
    }

    private static string Sha(string content) =>
        Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(content)))
        .ToLowerInvariant();
}

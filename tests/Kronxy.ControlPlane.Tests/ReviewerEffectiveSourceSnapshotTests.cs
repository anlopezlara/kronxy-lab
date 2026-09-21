using System.Security.Cryptography;
using System.Text;
using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;
using Kronxy.Infrastructure.Execution;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class ReviewerEffectiveSourceSnapshotTests
{
    private static readonly Guid JobId = Guid.NewGuid();
    private static readonly Guid RunId = Guid.NewGuid();
    private const string ModelPath =
        "src/Kronxy.Domain/ProjectModules/ProjectModule.cs";
    private const string ErrorsPath =
        "src/Kronxy.Domain/ProjectModules/ProjectModuleErrors.cs";
    private const string RepositoryPath =
        "src/Kronxy.Domain/ProjectModules/IProjectModuleRepository.cs";

    [Theory]
    [InlineData(DeveloperProposalLineage.Original)]
    [InlineData(DeveloperProposalLineage.BuildCorrection)]
    [InlineData(DeveloperProposalLineage.HumanReviewCorrection)]
    public async Task Captures_complete_planner_scope_for_every_effective_lineage(
        DeveloperProposalLineage lineage)
    {
        await WithRepository(async root =>
        {
            ReviewerEffectiveSourceSnapshotResult result = await Service().CaptureAsync(
                Request(root, lineage));

            Assert.True(result.IsSuccess);
            Assert.Equal(lineage, result.Snapshot!.EffectiveProposalLineage);
            Assert.Equal([ModelPath, ErrorsPath, RepositoryPath],
                result.Snapshot.Files.Select(file => file.RelativePath));
            foreach (string criterion in new[]
            {
                "Id", "ProjectId", "Name", "Description", "IsActive",
                "CreatedOnUtc", "UpdatedOnUtc", "DeletedOnUtc",
                "Create", "Update", "Activate", "Deactivate"
            })
                Assert.Contains(criterion, result.Snapshot.Files[0].Content);
            Assert.Contains("ProjectModule.HasActiveChildren", result.Snapshot.Files[1].Content);
            Assert.Contains("GetByIdAsync", result.Snapshot.Files[2].Content);
            Assert.Contains("Add", result.Snapshot.Files[2].Content);
            Assert.All(result.Snapshot.Files,
                file => Assert.Equal(64, file.Sha256.Length));
        });
    }

    [Fact]
    public async Task Path_outside_planner_allowlist_fails_closed()
    {
        await WithRepository(async root =>
        {
            ReviewerEffectiveSourceSnapshotRequest baseline = Request(
                root, DeveloperProposalLineage.HumanReviewCorrection);
            ReviewerEffectiveSourceSnapshotResult result = await Service().CaptureAsync(
                baseline with
                {
                    ObservedChanges = baseline.ObservedChanges with
                    {
                        Entries =
                        [
                            .. baseline.ObservedChanges.Entries,
                            new("src/Outside.cs", ObservedRepositoryChangeKind.Modified,
                                new string('a', 64), 1)
                        ]
                    }
                });

            Assert.Equal(ReviewerEffectiveSourceFailureKind.InvalidRequest,
                result.FailureKind);
            Assert.Equal("REVIEWER_EFFECTIVE_SOURCE_INVALID_REQUEST", result.ErrorCode);
        });
    }

    [Fact]
    public async Task Human_review_incremental_proposal_accepts_cumulative_observed_manifest()
    {
        await WithRepository(async root =>
        {
            ReviewerEffectiveSourceSnapshotRequest baseline = Request(
                root, DeveloperProposalLineage.HumanReviewCorrection);
            ReviewerEffectiveSourceSnapshotResult result = await Service().CaptureAsync(
                baseline with
                {
                    ObservedChanges = baseline.ObservedChanges with
                    {
                        Entries =
                        [
                            Entry(ModelPath, ModelContent),
                            Entry(ErrorsPath, ErrorsContent),
                            Entry(RepositoryPath, RepositoryContent)
                        ]
                    }
                });

            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Snapshot!.Files.Count);
            Assert.Equal(DeveloperProposalLineage.HumanReviewCorrection,
                result.Snapshot.EffectiveProposalLineage);
        });
    }

    [Fact]
    public async Task Proposal_path_missing_from_observed_manifest_fails_closed()
    {
        await WithRepository(async root =>
        {
            ReviewerEffectiveSourceSnapshotRequest baseline = Request(
                root, DeveloperProposalLineage.HumanReviewCorrection);
            ReviewerEffectiveSourceSnapshotResult result = await Service().CaptureAsync(
                baseline with
                {
                    ObservedChanges = baseline.ObservedChanges with { Entries = [] }
                });

            Assert.Equal(ReviewerEffectiveSourceFailureKind.InvalidRequest,
                result.FailureKind);
            Assert.Equal("REVIEWER_EFFECTIVE_SOURCE_INVALID_REQUEST", result.ErrorCode);
        });
    }

    [Fact]
    public async Task Missing_planned_source_fails_closed()
    {
        await WithRepository(async root =>
        {
            File.Delete(Path.Combine(root,
                RepositoryPath.Replace('/', Path.DirectorySeparatorChar)));

            ReviewerEffectiveSourceSnapshotResult result = await Service().CaptureAsync(
                Request(root, DeveloperProposalLineage.Original));

            Assert.Equal(ReviewerEffectiveSourceFailureKind.MissingSource,
                result.FailureKind);
            Assert.Equal("REVIEWER_EFFECTIVE_SOURCE_MISSING", result.ErrorCode);
        });
    }

    [Fact]
    public async Task Manifest_sha_that_does_not_match_final_source_fails_closed()
    {
        await WithRepository(async root =>
        {
            ReviewerEffectiveSourceSnapshotRequest baseline = Request(
                root, DeveloperProposalLineage.BuildCorrection);
            ReviewerEffectiveSourceSnapshotResult result = await Service().CaptureAsync(
                baseline with
                {
                    ObservedChanges = baseline.ObservedChanges with
                    {
                        Entries =
                        [
                            new(ErrorsPath, ObservedRepositoryChangeKind.Modified,
                                new string('f', 64), ErrorsContent.Length)
                        ]
                    }
                });

            Assert.Equal(ReviewerEffectiveSourceFailureKind.StaleSource,
                result.FailureKind);
            Assert.Equal("REVIEWER_EFFECTIVE_SOURCE_MANIFEST_MISMATCH", result.ErrorCode);
        });
    }

    private static IReviewerEffectiveSourceSnapshotService Service() =>
        new ReviewerEffectiveSourceSnapshotService();

    private static ReviewerEffectiveSourceSnapshotRequest Request(
        string root,
        DeveloperProposalLineage lineage)
    {
        string errorsSha = Hash(ErrorsContent);
        return new ReviewerEffectiveSourceSnapshotRequest
        {
            JobId = JobId,
            RunId = RunId,
            Repository = new RepositoryWorktreeHandle(
                JobId, "KRX-TEST", root, root, "test", new string('a', 40),
                RepositoryWorktreeOperationKind.Recovered),
            Plan = new PlannerPlan
            {
                Objective = "Review the complete ProjectModule domain feature.",
                FilesToInspect = [],
                CandidateFilesToModify = [ModelPath, ErrorsPath, RepositoryPath],
                Strategy = "Governed change",
                AcceptanceCriteria =
                [
                    "ProjectModule model is complete.",
                    "All five ProjectModule error codes exist.",
                    "Repository contract is complete."
                ],
                Risks = [], ExpectedTests = [], Assumptions = [], Uncertainties = []
            },
            EffectiveProposal = new ValidatedDeveloperProposal(
                "Effective correction",
                [
                    new ValidatedDeveloperChange(
                        DeveloperChangeOperationType.ReplaceFile,
                        ErrorsPath,
                        "Complete errors.",
                        ErrorsContent,
                        new string('b', 64),
                        Encoding.UTF8.GetByteCount(ErrorsContent))
                ],
                [], [], Encoding.UTF8.GetByteCount(ErrorsContent), 512),
            ObservedChanges = new ObservedChangeManifest(
                JobId, RunId, new string('a', 40),
                [
                    new(ErrorsPath, ObservedRepositoryChangeKind.Modified,
                        errorsSha, Encoding.UTF8.GetByteCount(ErrorsContent))
                ]),
            EffectiveProposalLineage = lineage
        };
    }

    private static async Task WithRepository(Func<string, Task> assertion)
    {
        string root = Path.Combine(Path.GetTempPath(),
            "kronxy-reviewer-source-" + Guid.NewGuid().ToString("N"));
        try
        {
            await Write(root, ModelPath, ModelContent);
            await Write(root, ErrorsPath, ErrorsContent);
            await Write(root, RepositoryPath, RepositoryContent);
            await assertion(root);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async Task Write(string root, string relativePath, string content)
    {
        string path = Path.Combine(root,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, new UTF8Encoding(false));
    }

    private static string Hash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)))
            .ToLowerInvariant();

    private static ObservedChangeManifestEntry Entry(string path, string content) =>
        new(path, ObservedRepositoryChangeKind.Modified,
            Hash(content), Encoding.UTF8.GetByteCount(content));

    private const string ErrorsContent =
        "ProjectModule.NotFound ProjectModule.WrongParent " +
        "ProjectModule.ParentNotFound ProjectModule.ParentInactive " +
        "ProjectModule.HasActiveChildren";

    private const string ModelContent =
        "Id ProjectId Name Description IsActive CreatedOnUtc UpdatedOnUtc DeletedOnUtc " +
        "Create Update Activate Deactivate";

    private const string RepositoryContent = "GetByIdAsync Add";
}

using Kronxy.Application.Artifacts;
using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;
using Kronxy.Infrastructure.Execution;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class BuildExecutionServiceTests
{
    [Fact]
    public async Task Success_WritesThreeArtifacts()
    {
        Fixture fixture =
            CreateFixture(
                ToolExecutionOutcome.Completed,
                0);

        BuildExecutionResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Request());

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            1,
            fixture.Executor.CallCount);

        Assert.Equal(
            3,
            fixture.ArtifactStore.Writes.Count);

        Assert.Contains(
            fixture.ArtifactStore.Writes,
            x => x.ArtifactType ==
                 ArtifactType.BuildReport);

        Assert.Contains(
            fixture.ArtifactStore.Writes,
            x => x.ArtifactType ==
                 ArtifactType.BuildStandardOutput);

        Assert.Contains(
            fixture.ArtifactStore.Writes,
            x => x.ArtifactType ==
                 ArtifactType.BuildStandardError);
    }

    [Fact]
    public async Task NonZeroExit_ReturnsBuildFailed()
    {
        Fixture fixture =
            CreateFixture(
                ToolExecutionOutcome.NonZeroExitCode,
                1);

        BuildExecutionResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Request());

        Assert.False(
            result.IsSuccess);

        Assert.Equal(
            BuildExecutionFailureKind.BuildFailed,
            result.FailureKind);

        Assert.Equal(
            3,
            fixture.ArtifactStore.Writes.Count);
    }

    [Fact]
    public async Task Rejected_ReturnsToolRejected()
    {
        Fixture fixture =
            CreateFixture(
                ToolExecutionOutcome.Rejected,
                null,
                "TOOL_REJECTED");

        BuildExecutionResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Request());

        Assert.Equal(
            BuildExecutionFailureKind.ToolRejected,
            result.FailureKind);
    }

    [Fact]
    public async Task Timeout_ReturnsTimedOut()
    {
        Fixture fixture =
            CreateFixture(
                ToolExecutionOutcome.TimedOut,
                null);

        BuildExecutionResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Request());

        Assert.Equal(
            BuildExecutionFailureKind.TimedOut,
            result.FailureKind);
    }

    [Fact]
    public async Task StructuredCancelled_ReturnsCancelled()
    {
        Fixture fixture =
            CreateFixture(
                ToolExecutionOutcome.Cancelled,
                null);

        BuildExecutionResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Request());

        Assert.Equal(
            BuildExecutionFailureKind.Cancelled,
            result.FailureKind);
    }

    [Fact]
    public async Task ArtifactFailure_ReturnsArtifactWriteFailure()
    {
        Fixture fixture =
            CreateFixture(
                ToolExecutionOutcome.Completed,
                0);

        fixture.ArtifactStore.FailOnWriteNumber =
            2;

        BuildExecutionResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Request());

        Assert.False(
            result.IsSuccess);

        Assert.Equal(
            BuildExecutionFailureKind.ArtifactWriteFailure,
            result.FailureKind);

        Assert.NotNull(
            result.StandardOutputArtifact);
    }

    [Fact]
    public async Task InvalidRequest_DoesNotInvokeExecutor()
    {
        Fixture fixture =
            CreateFixture(
                ToolExecutionOutcome.Completed,
                0);

        BuildExecutionRequest invalid =
            fixture.Request() with
            {
                JobId =
                    Guid.Empty
            };

        BuildExecutionResult result =
            await fixture.Service.ExecuteAsync(
                invalid);

        Assert.Equal(
            BuildExecutionFailureKind.InvalidRequest,
            result.FailureKind);

        Assert.Equal(
            0,
            fixture.Executor.CallCount);

        Assert.Empty(
            fixture.ArtifactStore.Writes);
    }

    [Fact]
    public async Task Correction_build_writes_only_separate_artifacts()
    {
        Fixture fixture = CreateFixture(
            ToolExecutionOutcome.Completed,
            0);

        BuildExecutionResult result = await fixture.Service.ExecuteAsync(
            fixture.Request() with { IsBuildCorrection = true });

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [
                ArtifactType.BuildCorrectionStandardOutput,
                ArtifactType.BuildCorrectionStandardError,
                ArtifactType.BuildCorrectionReport
            ],
            fixture.ArtifactStore.Writes.Select(item => item.ArtifactType));
        Assert.DoesNotContain(
            fixture.ArtifactStore.Writes,
            item => item.ArtifactType == ArtifactType.BuildReport);
    }

    private static Fixture CreateFixture(
        ToolExecutionOutcome outcome,
        int? exitCode,
        string errorCode = "")
    {
        Guid jobId =
            Guid.NewGuid();

        Guid runId =
            Guid.NewGuid();

        string workspace =
            "/tmp/kronxy-build-test/workspace";

        RepositoryWorktreeHandle repository =
            new(
                jobId,
                "KRX-BUILD-TEST",
                workspace,
                workspace + "/repo",
                "kronxy/job-build-test",
                "0123456789abcdef0123456789abcdef01234567",
                RepositoryWorktreeOperationKind.Existing);

        DateTime started =
            new DateTime(
                2026,
                8,
                29,
                12,
                0,
                0,
                DateTimeKind.Utc);

        SecureToolResult toolResult =
            new(
                outcome,
                exitCode,
                "build stdout",
                "build stderr",
                new ToolExecutionAudit(
                    jobId,
                    "KRX-BUILD-TEST",
                    SecureToolOperation.DotnetBuild,
                    workspace,
                    workspace + "/repo",
                    "build-test",
                    started,
                    started.AddSeconds(1),
                    TimeSpan.FromSeconds(1),
                    exitCode,
                    outcome),
                errorCode);

        StubSecureToolExecutor executor =
            new(
                toolResult);

        StubArtifactStore artifactStore =
            new();

        BuildExecutionService service =
            new(
                executor,
                artifactStore);

        return new Fixture(
            jobId,
            runId,
            repository,
            executor,
            artifactStore,
            service);
    }

    private sealed record Fixture(
        Guid JobId,
        Guid RunId,
        RepositoryWorktreeHandle Repository,
        StubSecureToolExecutor Executor,
        StubArtifactStore ArtifactStore,
        BuildExecutionService Service)
    {
        public BuildExecutionRequest Request() =>
            new()
            {
                JobId =
                    JobId,

                RunId =
                    RunId,

                Repository =
                    Repository,

                Target =
                    "Sample.csproj",

                CorrelationId =
                    "build-test"
            };
    }

    private sealed class StubSecureToolExecutor :
        ISecureToolExecutor
    {
        private readonly SecureToolResult result;

        public StubSecureToolExecutor(
            SecureToolResult result)
        {
            this.result =
                result;
        }

        public int CallCount
        {
            get;
            private set;
        }

        public Task<SecureToolResult> ExecuteAsync(
            SecureToolRequest request,
            CancellationToken cancellationToken =
                default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            CallCount++;

            Assert.Equal(
                SecureToolOperation.DotnetBuild,
                request.Operation);

            Assert.Equal(
                "Sample.csproj",
                request.Target);

            return Task.FromResult(
                result);
        }
    }

    private sealed class StubArtifactStore :
        IArtifactStore
    {
        public List<ArtifactWriteRequest> Writes
        {
            get;
        } = [];

        public int? FailOnWriteNumber
        {
            get;
            set;
        }

        public Task<ArtifactWriteResult> WriteAsync(
            ArtifactWriteRequest request,
            CancellationToken cancellationToken =
                default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            Writes.Add(
                request);

            int writeNumber =
                Writes.Count;

            if (FailOnWriteNumber ==
                writeNumber)
            {
                return Task.FromResult(
                    ArtifactWriteResult.Failure(
                        ArtifactStoreFailureKind.IoFailure,
                        "ARTIFACT_TEST_FAILURE"));
            }

            byte[] bytes =
                request.Content.ToArray();

            ArtifactRecord artifact =
                new()
                {
                    ArtifactId =
                        Guid.NewGuid(),

                    JobId =
                        request.JobId,

                    RunId =
                        request.RunId,

                    ArtifactType =
                        request.ArtifactType,

                    RelativePath =
                        $"{request.JobId:N}/" +
                        $"{request.RunId:N}/" +
                        $"{request.ArtifactType}.test",

                    Sha256 =
                        Convert.ToHexString(
                                System.Security.Cryptography
                                    .SHA256.HashData(
                                        bytes))
                            .ToLowerInvariant(),

                    SizeBytes =
                        bytes.LongLength,

                    CreatedAtUtc =
                        DateTimeOffset.UtcNow,

                    CorrelationId =
                        request.CorrelationId
                };

            return Task.FromResult(
                ArtifactWriteResult.Success(
                    artifact));
        }
    }
}

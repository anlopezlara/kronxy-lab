using System.Text;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;
using Kronxy.Infrastructure.Execution;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class RestoreExecutionServiceTests
{
    [Fact]
    public async Task Successful_restore_persists_all_evidence()
    {
        var fixture = new Fixture(
            ToolResult(
                ToolExecutionOutcome.Completed,
                0,
                "restore-ok",
                string.Empty,
                string.Empty));

        RestoreExecutionResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Request());

        Assert.True(result.IsSuccess);
        Assert.Equal(
            RestoreExecutionFailureKind.None,
            result.FailureKind);

        Assert.NotNull(
            result.ReportArtifact);

        Assert.NotNull(
            result.StandardOutputArtifact);

        Assert.NotNull(
            result.StandardErrorArtifact);

        Assert.Equal(
            3,
            fixture.ArtifactStore
                .Writes.Count);

        Assert.Equal(
            [
                ArtifactType.RestoreStandardOutput,
                ArtifactType.RestoreStandardError,
                ArtifactType.RestoreReport
            ],
            fixture.ArtifactStore
                .Writes
                .Select(
                    item =>
                        item.ArtifactType)
                .ToArray());
    }

    [Fact]
    public async Task Non_zero_restore_preserves_evidence_and_fails()
    {
        var fixture = new Fixture(
            ToolResult(
                ToolExecutionOutcome
                    .NonZeroExitCode,
                1,
                "restore-out",
                "restore-error",
                string.Empty));

        RestoreExecutionResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Request());

        Assert.False(result.IsSuccess);

        Assert.Equal(
            RestoreExecutionFailureKind
                .RestoreFailed,
            result.FailureKind);

        Assert.Equal(
            "RESTORE_NON_ZERO_EXIT_CODE",
            result.ErrorCode);

        Assert.NotNull(
            result.ReportArtifact);

        Assert.NotNull(
            result.StandardOutputArtifact);

        Assert.NotNull(
            result.StandardErrorArtifact);

        Assert.Equal(
            3,
            fixture.ArtifactStore
                .Writes.Count);
    }

    [Fact]
    public async Task Rejected_restore_preserves_evidence_and_maps_failure()
    {
        var fixture = new Fixture(
            ToolResult(
                ToolExecutionOutcome.Rejected,
                null,
                string.Empty,
                string.Empty,
                "TOOL_TARGET_INVALID"));

        RestoreExecutionResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Request());

        Assert.False(result.IsSuccess);

        Assert.Equal(
            RestoreExecutionFailureKind
                .ToolRejected,
            result.FailureKind);

        Assert.Equal(
            "TOOL_TARGET_INVALID",
            result.ErrorCode);

        Assert.Equal(
            3,
            fixture.ArtifactStore
                .Writes.Count);
    }

    [Fact]
    public async Task Timeout_preserves_evidence_and_maps_failure()
    {
        var fixture = new Fixture(
            ToolResult(
                ToolExecutionOutcome.TimedOut,
                null,
                "partial",
                "timeout",
                "TOOL_TIMED_OUT"));

        RestoreExecutionResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Request());

        Assert.False(result.IsSuccess);

        Assert.Equal(
            RestoreExecutionFailureKind
                .TimedOut,
            result.FailureKind);

        Assert.Equal(
            "TOOL_TIMED_OUT",
            result.ErrorCode);

        Assert.Equal(
            3,
            fixture.ArtifactStore
                .Writes.Count);
    }

    [Fact]
    public async Task Tool_cancelled_preserves_evidence_and_maps_failure()
    {
        var fixture = new Fixture(
            ToolResult(
                ToolExecutionOutcome.Cancelled,
                null,
                string.Empty,
                string.Empty,
                "TOOL_CANCELLED"));

        RestoreExecutionResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Request());

        Assert.False(result.IsSuccess);

        Assert.Equal(
            RestoreExecutionFailureKind
                .Cancelled,
            result.FailureKind);

        Assert.Equal(
            "TOOL_CANCELLED",
            result.ErrorCode);

        Assert.Equal(
            3,
            fixture.ArtifactStore
                .Writes.Count);
    }

    [Fact]
    public async Task Artifact_failure_is_fail_closed_but_keeps_previous_artifacts()
    {
        var fixture = new Fixture(
            ToolResult(
                ToolExecutionOutcome.Completed,
                0,
                "restore-ok",
                string.Empty,
                string.Empty));

        fixture.ArtifactStore.FailOnWriteNumber =
            2;

        RestoreExecutionResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Request());

        Assert.False(result.IsSuccess);

        Assert.Equal(
            RestoreExecutionFailureKind
                .ArtifactWriteFailure,
            result.FailureKind);

        Assert.Equal(
            "ARTIFACT_TEST_FAILURE",
            result.ErrorCode);

        Assert.NotNull(
            result.StandardOutputArtifact);

        Assert.Null(
            result.StandardErrorArtifact);

        Assert.NotNull(
            result.ReportArtifact);

        Assert.Equal(
            3,
            fixture.ArtifactStore
                .Writes.Count);
    }

    [Fact]
    public async Task Invalid_request_does_not_execute_or_write()
    {
        var fixture = new Fixture(
            ToolResult(
                ToolExecutionOutcome.Completed,
                0,
                string.Empty,
                string.Empty,
                string.Empty));

        RestoreExecutionRequest request =
            fixture.Request() with
            {
                JobId = Guid.Empty
            };

        RestoreExecutionResult result =
            await fixture.Service.ExecuteAsync(
                request);

        Assert.False(result.IsSuccess);

        Assert.Equal(
            RestoreExecutionFailureKind
                .InvalidRequest,
            result.FailureKind);

        Assert.Equal(
            0,
            fixture.Executor
                .CallCount);

        Assert.Empty(
            fixture.ArtifactStore
                .Writes);
    }

    private static SecureToolResult ToolResult(
        ToolExecutionOutcome outcome,
        int? exitCode,
        string stdout,
        string stderr,
        string errorCode)
    {
        DateTime started =
            DateTime.UtcNow;

        return new SecureToolResult(
            outcome,
            exitCode,
            stdout,
            stderr,
            new ToolExecutionAudit(
                Guid.NewGuid(),
                "JOB-RESTORE-TEST",
                SecureToolOperation.DotnetRestore,
                "/tmp/kronxy-test-workspace",
                "/tmp/kronxy-test-workspace/repository",
                "restore-test",
                started,
                started.AddMilliseconds(10),
                TimeSpan.FromMilliseconds(10),
                exitCode,
                outcome),
            errorCode);
    }

    private sealed class Fixture
    {
        public Fixture(
            SecureToolResult result)
        {
            Executor =
                new StubSecureToolExecutor(
                    result);

            ArtifactStore =
                new StubArtifactStore();

            Service =
                new RestoreExecutionService(
                    Executor,
                    ArtifactStore);

            JobId =
                Guid.NewGuid();

            RunId =
                Guid.NewGuid();

            Repository =
                new RepositoryWorktreeHandle(
                    JobId,
                    "JOB-RESTORE-TEST",
                    "/tmp/kronxy-test-workspace",
                    "/tmp/kronxy-test-workspace/repository",
                    "main",
                    new string('a', 40),
                    RepositoryWorktreeOperationKind
                        .Existing);
        }

        public Guid JobId { get; }

        public Guid RunId { get; }

        public RepositoryWorktreeHandle
            Repository { get; }

        public StubSecureToolExecutor
            Executor { get; }

        public StubArtifactStore
            ArtifactStore { get; }

        public RestoreExecutionService
            Service { get; }

        public RestoreExecutionRequest Request() =>
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
                    "restore-test"
            };
    }

    private sealed class StubSecureToolExecutor :
        ISecureToolExecutor
    {
        private readonly SecureToolResult result;

        public StubSecureToolExecutor(
            SecureToolResult result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public Task<SecureToolResult> ExecuteAsync(
            SecureToolRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            Assert.Equal(
                SecureToolOperation.DotnetRestore,
                request.Operation);

            return Task.FromResult(
                result);
        }
    }

    private sealed class StubArtifactStore :
        IArtifactStore
    {
        public List<ArtifactWriteRequest>
            Writes { get; } = [];

        public int? FailOnWriteNumber
        {
            get;
            set;
        }

        public Task<ArtifactWriteResult> WriteAsync(
            ArtifactWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            Writes.Add(
                request);

            int writeNumber =
                Writes.Count;

            if (FailOnWriteNumber ==
                writeNumber)
            {
                return Task.FromResult(
                    ArtifactWriteResult.Failure(
                        ArtifactStoreFailureKind
                            .IoFailure,
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

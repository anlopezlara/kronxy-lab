using Kronxy.Application.Artifacts;
using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;
using Kronxy.Infrastructure.Artifacts;
using Kronxy.Infrastructure.Execution;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class TestExecutionServiceTests
{
    [Fact]
    public async Task Success_WritesFourArtifacts()
    {
        using Fixture fixture =
            CreateFixture(
                ToolExecutionOutcome.Completed,
                0,
                createTrx: true);

        TestExecutionResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Request());

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            1,
            fixture.Executor.CallCount);

        Assert.Equal(
            4,
            fixture.ArtifactStore.Writes.Count);

        Assert.Contains(
            fixture.ArtifactStore.Writes,
            x => x.ArtifactType ==
                 ArtifactType.TestReport);

        Assert.Contains(
            fixture.ArtifactStore.Writes,
            x => x.ArtifactType ==
                 ArtifactType.TestResults);

        Assert.Contains(
            fixture.ArtifactStore.Writes,
            x => x.ArtifactType ==
                 ArtifactType.TestStandardOutput);

        Assert.Contains(
            fixture.ArtifactStore.Writes,
            x => x.ArtifactType ==
                 ArtifactType.TestStandardError);
    }

    [Fact]
    public async Task NonZeroExit_PersistsTrx_AndReturnsTestsFailed()
    {
        using Fixture fixture =
            CreateFixture(
                ToolExecutionOutcome.NonZeroExitCode,
                1,
                createTrx: true);

        TestExecutionResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Request());

        Assert.False(
            result.IsSuccess);

        Assert.Equal(
            TestExecutionFailureKind.TestsFailed,
            result.FailureKind);

        Assert.NotNull(
            result.ResultsArtifact);

        Assert.Equal(
            4,
            fixture.ArtifactStore.Writes.Count);
    }

    [Fact]
    public async Task MissingTrx_ReturnsTestResultsMissing()
    {
        using Fixture fixture =
            CreateFixture(
                ToolExecutionOutcome.Completed,
                0,
                createTrx: false);

        TestExecutionResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Request());

        Assert.False(
            result.IsSuccess);

        Assert.Equal(
            TestExecutionFailureKind.TestResultsMissing,
            result.FailureKind);

        Assert.Null(
            result.ResultsArtifact);

        Assert.Equal(
            3,
            fixture.ArtifactStore.Writes.Count);
    }

    [Fact]
    public async Task SymlinkTrx_ReturnsTestResultsUnsafe()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using Fixture fixture =
            CreateFixture(
                ToolExecutionOutcome.Completed,
                0,
                createTrx: false);

        string resultsDirectory =
            fixture.ResultsDirectory;

        Directory.CreateDirectory(
            resultsDirectory);

        string outside =
            Path.Combine(
                fixture.Root,
                "outside.trx");

        File.WriteAllText(
            outside,
            "<TestRun />");

        string trx =
            Path.Combine(
                resultsDirectory,
                "kronxy-tests.trx");

        File.CreateSymbolicLink(
            trx,
            outside);

        TestExecutionResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Request());

        Assert.False(
            result.IsSuccess);

        Assert.Equal(
            TestExecutionFailureKind.TestResultsUnsafe,
            result.FailureKind);

        Assert.Equal(
            "TEST_RESULTS_PATH_UNSAFE",
            result.ErrorCode);
    }

    [Fact]
    public async Task Timeout_DoesNotRequireTrx()
    {
        using Fixture fixture =
            CreateFixture(
                ToolExecutionOutcome.TimedOut,
                null,
                createTrx: false);

        TestExecutionResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Request());

        Assert.Equal(
            TestExecutionFailureKind.TimedOut,
            result.FailureKind);

        Assert.Equal(
            3,
            fixture.ArtifactStore.Writes.Count);

        Assert.Null(
            result.ResultsArtifact);
    }

    [Fact]
    public async Task StructuredCancelled_ReturnsCancelled()
    {
        using Fixture fixture =
            CreateFixture(
                ToolExecutionOutcome.Cancelled,
                null,
                createTrx: false);

        TestExecutionResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Request());

        Assert.Equal(
            TestExecutionFailureKind.Cancelled,
            result.FailureKind);
    }

    [Fact]
    public async Task ArtifactFailure_ReturnsArtifactWriteFailure()
    {
        using Fixture fixture =
            CreateFixture(
                ToolExecutionOutcome.Completed,
                0,
                createTrx: true);

        fixture.ArtifactStore.FailOnWriteNumber =
            4;

        TestExecutionResult result =
            await fixture.Service.ExecuteAsync(
                fixture.Request());

        Assert.False(
            result.IsSuccess);

        Assert.Equal(
            TestExecutionFailureKind.ArtifactWriteFailure,
            result.FailureKind);

        Assert.NotNull(
            result.ReportArtifact);

        Assert.Null(
            result.ResultsArtifact);
    }

    [Fact]
    public async Task InvalidRequest_DoesNotInvokeExecutor()
    {
        using Fixture fixture =
            CreateFixture(
                ToolExecutionOutcome.Completed,
                0,
                createTrx: true);

        TestExecutionRequest invalid =
            fixture.Request() with
            {
                JobId =
                    Guid.Empty
            };

        TestExecutionResult result =
            await fixture.Service.ExecuteAsync(
                invalid);

        Assert.Equal(
            TestExecutionFailureKind.InvalidRequest,
            result.FailureKind);

        Assert.Equal(
            0,
            fixture.Executor.CallCount);

        Assert.Empty(
            fixture.ArtifactStore.Writes);
    }

    private static Fixture CreateFixture(
        ToolExecutionOutcome outcome,
        int? exitCode,
        bool createTrx,
        string errorCode = "")
    {
        string root =
            Path.Combine(
                Path.GetTempPath(),
                "kronxy-test-execution-" +
                Guid.NewGuid().ToString("N"));

        string workspace =
            Path.Combine(
                root,
                "workspace");

        string repositoryPath =
            Path.Combine(
                workspace,
                "repository");

        Directory.CreateDirectory(
            repositoryPath);

        Guid jobId =
            Guid.NewGuid();

        Guid runId =
            Guid.NewGuid();

        RepositoryWorktreeHandle repository =
            new(
                jobId,
                "KRX-TEST-EXEC",
                workspace,
                repositoryPath,
                "kronxy/job-test",
                "0123456789abcdef0123456789abcdef01234567",
                RepositoryWorktreeOperationKind.Existing);

        string resultsDirectory =
            Path.Combine(
                workspace,
                ".kronxy",
                "test-results");

        if (createTrx)
        {
            Directory.CreateDirectory(
                resultsDirectory);

            File.WriteAllText(
                Path.Combine(
                    resultsDirectory,
                    "kronxy-tests.trx"),
                "<TestRun id=\"KRONXY\" />");
        }

        DateTime started =
            new(
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
                "test stdout",
                "test stderr",
                new ToolExecutionAudit(
                    jobId,
                    "KRX-TEST-EXEC",
                    SecureToolOperation.DotnetTest,
                    workspace,
                    repositoryPath,
                    "test-correlation",
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

        ArtifactStoreOptions artifactOptions =
            new()
            {
                RootPath =
                    Path.Combine(
                        root,
                        "artifacts"),

                MaxArtifactBytes =
                    16 * 1024 * 1024
            };

        TestExecutionService service =
            new(
                executor,
                artifactStore,
                artifactOptions);

        return new Fixture(
            root,
            resultsDirectory,
            jobId,
            runId,
            repository,
            executor,
            artifactStore,
            service);
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

        public int CallCount { get; private set; }

        public Task<SecureToolResult> ExecuteAsync(
            SecureToolRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            Assert.Equal(
                SecureToolOperation.DotnetTest,
                request.Operation);

            Assert.Equal(
                "Kronxy.sln",
                request.Target);

            return Task.FromResult(
                result);
        }
    }

    private sealed class StubArtifactStore :
        IArtifactStore
    {
        public List<ArtifactWriteRequest> Writes { get; } =
            [];

        public int? FailOnWriteNumber { get; set; }

        public Task<ArtifactWriteResult> WriteAsync(
            ArtifactWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            Writes.Add(
                request);

            if (FailOnWriteNumber ==
                Writes.Count)
            {
                return Task.FromResult(
                    ArtifactWriteResult.Failure(
                        ArtifactStoreFailureKind.IoFailure,
                        "ARTIFACT_TEST_FAILURE"));
            }

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
                        request.ArtifactType.ToString(),

                    Sha256 =
                        "0123456789abcdef",

                    SizeBytes =
                        request.Content.Length,

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

    private sealed record Fixture(
        string Root,
        string ResultsDirectory,
        Guid JobId,
        Guid RunId,
        RepositoryWorktreeHandle Repository,
        StubSecureToolExecutor Executor,
        StubArtifactStore ArtifactStore,
        TestExecutionService Service) :
        IDisposable
    {
        public TestExecutionRequest Request() =>
            new()
            {
                JobId =
                    JobId,

                RunId =
                    RunId,

                Repository =
                    Repository,

                Target =
                    "Kronxy.sln",

                CorrelationId =
                    "test-correlation"
            };

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(
                        Root))
                {
                    Directory.Delete(
                        Root,
                        recursive: true);
                }
            }
            catch
            {
                // Best effort test cleanup.
            }
        }
    }
}

using System.Diagnostics;
using Kronxy.Application;
using Kronxy.Application.Execution;
using Kronxy.Application.Jobs;
using Kronxy.Domain.Jobs;
using Kronxy.Infrastructure;
using Kronxy.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Kronxy.Application.AI;

namespace Kronxy.ControlPlane.Tests;

public sealed class ExecutionPlaneEndToEndTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable(
            "ConnectionStrings__Database")
        ?? throw new InvalidOperationException(
            "ConnectionStrings__Database is required " +
            "for ExecutionPlaneEndToEndTests.");

    [Fact]
    public async Task
        Restart_preserves_pinned_commit_when_authoritative_repository_advances()
    {
        string container =
            Path.Combine(
                Path.GetTempPath(),
                "kronxy-e2e-execution-plane",
                Guid.NewGuid().ToString("N"));

        string repository =
            Path.Combine(
                container,
                "repository");

        string workspaceRoot =
            Path.Combine(
                container,
                "workspaces");

        Guid jobId =
            Guid.Empty;

        string? externalId =
            null;

        string? pinnedHead =
            null;

        try
        {
            Directory.CreateDirectory(
                repository);

            Directory.CreateDirectory(
                workspaceRoot);

            InitializeRepository(
                repository);

            string commitA =
                Git(
                    repository,
                    "rev-parse",
                    "HEAD")
                .Trim()
                .ToLowerInvariant();

            Assert.True(
                IsCommitHash(commitA));

            // -------------------------------------------------
            // PROVIDER / PROCESS 1
            //
            // Created
            //   -> ContextBuilding
            //   -> Planning
            //   -> WorkspacePreparing
            //
            // Planning must persist commit A.
            // -------------------------------------------------

            await using (
                ServiceProvider provider =
                    BuildProvider(
                        repository,
                        workspaceRoot))
            {
                await using AsyncServiceScope scope =
                    provider.CreateAsyncScope();

                IJobService jobService =
                    scope.ServiceProvider
                        .GetRequiredService<IJobService>();

                IJobOrchestrator orchestrator =
                    scope.ServiceProvider
                        .GetRequiredService<IJobOrchestrator>();

                var created =
                    await jobService.CreateAsync(
                        "Execution Plane E2E pinned revision test");

                Assert.True(
                    created.IsSuccess);

                jobId =
                    created.Value.Id;

                externalId =
                    created.Value.ExternalId;

                Assert.False(
                    string.IsNullOrWhiteSpace(
                        externalId));

                Assert.Equal(
                    JobState.Created,
                    created.Value.State);

                JobOperationResult first =
                    await orchestrator.AdvanceAsync(
                        jobId,
                        "e2e-test",
                        "e2e-created-context");

                Assert.True(
                    first.IsSuccess);

                Assert.Equal(
                    JobState.ContextBuilding,
                    created.Value.State);

                JobOperationResult second =
                    await orchestrator.AdvanceAsync(
                        jobId,
                        "e2e-test",
                        "e2e-context-planning");

                Assert.True(
                    second.IsSuccess,
                    $"Second advance failed. Kind={second.Kind}; Error={second.Error.Code}; Name={second.Error.Name}");

                Assert.Equal(
                    JobState.Planning,
                    created.Value.State);

                JobOperationResult third =
                    await orchestrator.AdvanceAsync(
                        jobId,
                        "e2e-test",
                        "e2e-planning-workspace");

                Assert.True(
                    third.IsSuccess,
                    $"Third advance failed. Kind={third.Kind}; Error={third.Error.Code}; Name={third.Error.Name}");

                Assert.Equal(
                    JobState.WorkspacePreparing,
                    created.Value.State);

                Assert.Equal(
                    commitA,
                    created.Value.BaseRepositoryHead);

                pinnedHead =
                    created.Value.BaseRepositoryHead;
            }

            Assert.NotNull(
                pinnedHead);

            // -------------------------------------------------
            // AUTHORITATIVE REPOSITORY ADVANCES TO B
            //
            // The Job must remain pinned to A.
            // -------------------------------------------------

            File.AppendAllText(
                Path.Combine(
                    repository,
                    "tracked.txt"),
                Environment.NewLine +
                "authoritative repository advanced");

            Git(
                repository,
                "add",
                "--all",
                "--");

            Git(
                repository,
                "commit",
                "-m",
                "commit B after job pin");

            string commitB =
                Git(
                    repository,
                    "rev-parse",
                    "HEAD")
                .Trim()
                .ToLowerInvariant();

            Assert.True(
                IsCommitHash(commitB));

            Assert.NotEqual(
                pinnedHead,
                commitB);

            // -------------------------------------------------
            // PROVIDER / PROCESS 2
            //
            // Simulates restart:
            // - new ServiceProvider
            // - new DbContext
            // - Job reloaded from PostgreSQL
            // - execution plane creates worktree at pinned A
            // -------------------------------------------------

            await using (
                ServiceProvider provider =
                    BuildProvider(
                        repository,
                        workspaceRoot))
            {
                await using AsyncServiceScope scope =
                    provider.CreateAsyncScope();

                IJobService jobService =
                    scope.ServiceProvider
                        .GetRequiredService<IJobService>();

                IJobOrchestrator orchestrator =
                    scope.ServiceProvider
                        .GetRequiredService<IJobOrchestrator>();

                IExecutionPlaneLifecycle lifecycle =
                    scope.ServiceProvider
                        .GetRequiredService<
                            IExecutionPlaneLifecycle>();

                var rehydrated =
                    await jobService.GetAsync(
                        jobId);

                Assert.True(
                    rehydrated.IsSuccess);

                Assert.Equal(
                    JobState.WorkspacePreparing,
                    rehydrated.Value.State);

                Assert.Equal(
                    pinnedHead,
                    rehydrated.Value.BaseRepositoryHead);

                JobOperationResult advance =
                    await orchestrator.AdvanceAsync(
                        jobId,
                        "e2e-test",
                        "e2e-workspace-developing");

                Assert.True(
                    advance.IsSuccess);

                Assert.Equal(
                    JobState.Developing,
                    rehydrated.Value.State);

                Assert.Equal(
                    pinnedHead,
                    rehydrated.Value.BaseRepositoryHead);

                ExecutionPlaneLifecycleResult recovered =
                    await lifecycle.RecoverAsync(
                        jobId,
                        externalId!);

                Assert.True(
                    recovered.IsSuccess);

                Assert.NotNull(
                    recovered.Session);

                Assert.Equal(
                    pinnedHead,
                    recovered.Session!
                        .Repository.Head);

                string actualWorktreeHead =
                    Git(
                        recovered.Session.Repository.RepositoryPath,
                        "rev-parse",
                        "HEAD")
                    .Trim()
                    .ToLowerInvariant();

                Assert.Equal(
                    pinnedHead,
                    actualWorktreeHead);

                Assert.NotEqual(
                    commitB,
                    actualWorktreeHead);

                ExecutionPlaneLifecycleResult cleanup =
                    await lifecycle.CleanupAsync(
                        jobId,
                        externalId!);

                Assert.True(
                    cleanup.IsSuccess);
            }

            // -------------------------------------------------
            // PROVE CLEANUP REMOVED OWNED WORKSPACE
            // WHILE PRESERVING AUDIT ARTIFACTS
            // -------------------------------------------------

            string artifactRoot =
                Path.Combine(
                    workspaceRoot,
                    "artifacts");

            string[] remainingDirectories =
                Directory
                    .EnumerateDirectories(
                        workspaceRoot)
                    .Select(
                        Path.GetFullPath)
                    .ToArray();

            Assert.DoesNotContain(
                remainingDirectories,
                directory =>
                    !string.Equals(
                        directory,
                        Path.GetFullPath(
                            artifactRoot),
                        OperatingSystem.IsWindows()
                            ? StringComparison.OrdinalIgnoreCase
                            : StringComparison.Ordinal));

            Assert.True(
                Directory.Exists(
                    artifactRoot));

            // -------------------------------------------------
            // FINAL DATABASE ASSERTION THROUGH NEW CONTEXT
            // -------------------------------------------------

            await using (
                ServiceProvider provider =
                    BuildProvider(
                        repository,
                        workspaceRoot))
            {
                await using AsyncServiceScope scope =
                    provider.CreateAsyncScope();

                IJobService jobService =
                    scope.ServiceProvider
                        .GetRequiredService<IJobService>();

                var finalJob =
                    await jobService.GetAsync(
                        jobId);

                Assert.True(
                    finalJob.IsSuccess);

                Assert.Equal(
                    JobState.Developing,
                    finalJob.Value.State);

                Assert.Equal(
                    pinnedHead,
                    finalJob.Value.BaseRepositoryHead);
            }
        }
        finally
        {
            if (jobId != Guid.Empty)
            {
                await DeleteJobAsync(
                    jobId,
                    repository,
                    workspaceRoot);
            }

            TryCleanupExecutionPlane(
                jobId,
                externalId,
                repository,
                workspaceRoot);

            if (Directory.Exists(
                    container))
            {
                Directory.Delete(
                    container,
                    recursive: true);
            }
        }
    }

    private static ServiceProvider BuildProvider(
        string repository,
        string workspaceRoot)
    {
        string providerArtifactRoot =
            Path.Combine(
                workspaceRoot,
                "artifacts");

        Directory.CreateDirectory(
            providerArtifactRoot);

        IConfiguration configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Database"] =
                            ConnectionString,

                        ["AI:Provider"] =
                            "Ollama",

                        ["AI:Endpoint"] =
                            "http://example.invalid",

                        ["AI:Models:CodingFast"] =
                            "model-fast",

                        ["AI:Models:CodingQuality"] =
                            "model-quality",

                        ["AI:Models:General"] =
                            "model-general",

                        ["AI:MaxConcurrentInferences"] =
                            "1",

                        ["AI:ConnectionTimeout"] =
                            "00:00:01",

                        ["AI:InferenceTimeout"] =
                            "00:00:05",

                        ["AI:PlanningInferenceTimeout"] =
                            "00:00:05",

                        ["AI:DeveloperInferenceTimeout"] =
                            "00:00:05",

                        ["AI:QueueWaitTimeout"] =
                            "00:00:01",

                        ["AI:MaxOutputTokens"] =
                            "256",

                        ["AI:MaxInputCharacters"] =
                            "65536",

                        ["AI:MaxResponseBytes"] =
                            "1048576",

                        ["ExecutionPlane:RepositoryRoot"] =
                            repository,

                        ["ExecutionPlane:WorkspaceRoot"] =
                            workspaceRoot,

                        ["ExecutionPlane:DotnetExecutable"] =
                            TestToolResolver.Dotnet(),

                        ["ExecutionPlane:GitExecutable"] =
                            TestToolResolver.Git(),

                        ["ExecutionPlane:DotnetTarget"] =
                            "Kronxy.sln",

                        ["ArtifactStore:RootPath"] =
                            providerArtifactRoot,

                        ["ArtifactStore:MaxArtifactBytes"] =
                            "16777216"
                    })
                .Build();

        ServiceCollection services =
            new();

        services.AddLogging();

        services.AddApplication();

        services.AddSingleton(
            new Kronxy.Context.Configuration.ContextOptions());

        services.AddInfrastructure(
            configuration);

        services.AddSingleton<
            IAiGateway,
            E2eAiGateway>();

        return services
            .BuildServiceProvider();
    }

    private static void InitializeRepository(
        string repository)
    {
        Git(
            repository,
            "init",
            "-b",
            "main");

        Git(
            repository,
            "config",
            "user.name",
            "KRONXY Tests");

        Git(
            repository,
            "config",
            "user.email",
            "kronxy@example.invalid");

        File.WriteAllText(
            Path.Combine(
                repository,
                "revision.txt"),
            "commit A");

        Git(
            repository,
            "add",
            "--all",
            "--");

        Git(
            repository,
            "commit",
            "-m",
            "commit A");
    }

    private static async Task DeleteJobAsync(
        Guid jobId,
        string repository,
        string workspaceRoot)
    {
        if (!Directory.Exists(
                repository) ||
            !Directory.Exists(
                workspaceRoot))
        {
            return;
        }

        await using ServiceProvider provider =
            BuildProvider(
                repository,
                workspaceRoot);

        await using AsyncServiceScope scope =
            provider.CreateAsyncScope();

        ApplicationDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<
                    ApplicationDbContext>();

        await dbContext.Database
            .ExecuteSqlInterpolatedAsync(
                $"DELETE FROM jobs WHERE id = {jobId}");
    }

    private static void TryCleanupExecutionPlane(
        Guid jobId,
        string? externalId,
        string repository,
        string workspaceRoot)
    {
        if (jobId == Guid.Empty ||
            string.IsNullOrWhiteSpace(
                externalId) ||
            !Directory.Exists(
                repository) ||
            !Directory.Exists(
                workspaceRoot))
        {
            return;
        }

        try
        {
            using ServiceProvider provider =
                BuildProvider(
                    repository,
                    workspaceRoot);

            using IServiceScope scope =
                provider.CreateScope();

            IExecutionPlaneLifecycle lifecycle =
                scope.ServiceProvider
                    .GetRequiredService<
                        IExecutionPlaneLifecycle>();

            _ =
                lifecycle.CleanupAsync(
                        jobId,
                        externalId)
                    .GetAwaiter()
                    .GetResult();
        }
        catch
        {
            // Test cleanup is best-effort.
            // The temporary container is removed afterwards.
        }
    }

    private sealed class E2eAiGateway :
        IAiGateway
    {
        public Task<AiResponse> GenerateAsync(
            AiRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new AiResponse
                {
                    Status =
                        AiOperationStatus.Success,

                    Content =
                        """
                        {
                          "objective": "Execution Plane E2E pinned revision test",
                          "filesToInspect": ["revision.txt"],
                          "candidateFilesToModify": [],
                          "strategy": "Preserve the pinned source revision through restart.",
                          "acceptanceCriteria": [
                            "The pinned commit remains unchanged.",
                            "The execution pipeline advances successfully."
                          ],
                          "risks": [
                            "The authoritative repository may advance during recovery."
                          ],
                          "expectedTests": [
                            "Run the deterministic execution-plane end-to-end test."
                          ],
                          "assumptions": [
                            "The temporary Git repositories remain available."
                          ],
                          "uncertainties": []
                        }
                        """,

                    Provider =
                        "E2E",

                    LogicalModel =
                        "CodingQuality",

                    PhysicalModel =
                        "e2e-deterministic",

                    Duration =
                        TimeSpan.Zero,

                    TerminationReason =
                        AiTerminationReason.Stop,

                    Usage =
                        new AiUsage(
                            10,
                            5)
                });
        }

        public Task<AiProviderHealthResult>
            CheckHealthAsync(
                CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new AiProviderHealthResult(
                    AiProviderHealthStatus.Available,
                    "E2E",
                    Array.Empty<string>(),
                    string.Empty));
        }
    }

    private static string Git(
        string workingDirectory,
        params string[] arguments)
    {
        ProcessStartInfo startInfo =
            new()
            {
                FileName =
                    TestToolResolver.Git(),

                WorkingDirectory =
                    workingDirectory,

                UseShellExecute =
                    false,

                RedirectStandardOutput =
                    true,

                RedirectStandardError =
                    true,

                CreateNoWindow =
                    true
            };

        foreach (
            string argument
            in arguments)
        {
            startInfo.ArgumentList.Add(
                argument);
        }

        using Process process =
            Process.Start(
                startInfo)
            ?? throw new InvalidOperationException(
                "Unable to start git.");

        string stdout =
            process.StandardOutput
                .ReadToEnd();

        string stderr =
            process.StandardError
                .ReadToEnd();

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git failed: {stderr}");
        }

        return stdout;
    }

    private static bool IsCommitHash(
        string value)
    {
        if (value.Length != 40 &&
            value.Length != 64)
        {
            return false;
        }

        foreach (char character
                 in value)
        {
            if (!Uri.IsHexDigit(
                    character))
            {
                return false;
            }
        }

        return true;
    }
}

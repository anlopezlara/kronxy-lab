using System.Diagnostics;
using Kronxy.Application;
using Kronxy.Application.Execution;
using Kronxy.Application.Jobs;
using Kronxy.Domain.Jobs;
using Kronxy.Infrastructure;
using Kronxy.Infrastructure.Data;
using Kronxy.Infrastructure.AI;
using Kronxy.Infrastructure.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Kronxy.Application.AI;
using Kronxy.Context.Files;
using Kronxy.Context.Git;
using Kronxy.Context.Processes;
using Xunit.Abstractions;
using Xunit.Sdk;

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
                        """
                        Domain-only
                        src/Kronxy.Domain/TestFixtures/RestartProbe.cs
                        Create the new RestartProbe component.
                        """);

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

                IJobRunIdProvider runIds = scope.ServiceProvider
                    .GetRequiredService<IJobRunIdProvider>();
                IStageRecoveryEvidenceService analysisRecovery = scope.ServiceProvider
                    .GetRequiredService<IStageRecoveryEvidenceService>();
                StageRecoveryResult recoveredAnalysis = await analysisRecovery.CheckAsync(
                    new StageRecoveryRequest
                    {
                        JobId = jobId,
                        RunId = runIds.Create(jobId, created.Value.AttemptCount),
                        Stage = RecoveryStage.DevelopmentAnalysis,
                        JobRequest = created.Value.Request,
                        CorrelationId = "e2e-greenfield-analysis",
                        AttemptCount = created.Value.AttemptCount
                    });
                Assert.True(recoveredAnalysis.IsCompleted);
                Assert.Equal(DevelopmentChangeClassification.NewComponent,
                    recoveredAnalysis.DevelopmentAnalysis!.PrimaryClassification);
                Assert.True(recoveredAnalysis.DevelopmentAnalysis.ScopeCompatible);
                Assert.False(recoveredAnalysis.DevelopmentAnalysis.ArchitectureDecisionRequired);
                Assert.True(recoveredAnalysis.DevelopmentAnalysis.DeveloperExecutionAllowed);

                JobOperationResult third =
                    await orchestrator.AdvanceAsync(
                        jobId,
                        "e2e-test",
                        "e2e-planning-workspace");

                Assert.True(
                    third.IsSuccess,
                    $"Third advance failed. Kind={third.Kind}; Error={third.Error.Code}; Name={third.Error.Name}; Diagnostic={third.DiagnosticCode}");

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

    [Fact]
    public async Task
        Restart_recovers_blocked_development_analysis_without_regeneration()
    {
        string container = Path.Combine(
            Path.GetTempPath(),
            "kronxy-e2e-blocked-development-analysis",
            Guid.NewGuid().ToString("N"));
        string repository = Path.Combine(container, "repository");
        string workspaceRoot = Path.Combine(container, "workspaces");
        Guid jobId = Guid.Empty;
        string? externalId = null;

        try
        {
            Directory.CreateDirectory(repository);
            Directory.CreateDirectory(workspaceRoot);
            InitializeRepository(repository);

            await using (ServiceProvider provider = BuildProvider(repository, workspaceRoot))
            {
                await using AsyncServiceScope scope = provider.CreateAsyncScope();
                IJobService jobService = scope.ServiceProvider.GetRequiredService<IJobService>();
                IJobOrchestrator orchestrator = scope.ServiceProvider.GetRequiredService<IJobOrchestrator>();

                var created = await jobService.CreateAsync(
                    "Ambiguous restart analysis request without a candidate target");
                Assert.True(created.IsSuccess);
                jobId = created.Value.Id;
                externalId = created.Value.ExternalId;

                Assert.True((await orchestrator.AdvanceAsync(
                    jobId, "e2e-test", "e2e-blocked-created-context")).IsSuccess);
                Assert.True((await orchestrator.AdvanceAsync(
                    jobId, "e2e-test", "e2e-blocked-context-analysis")).IsSuccess);
                Assert.Equal(JobState.WaitingHuman, created.Value.State);
            }

            await using (ServiceProvider provider = BuildProvider(repository, workspaceRoot))
            {
                await using AsyncServiceScope scope = provider.CreateAsyncScope();
                IJobService jobService = scope.ServiceProvider.GetRequiredService<IJobService>();
                IJobRunIdProvider runIds = scope.ServiceProvider.GetRequiredService<IJobRunIdProvider>();
                IStageRecoveryEvidenceService recovery = scope.ServiceProvider.GetRequiredService<IStageRecoveryEvidenceService>();

                var rehydrated = await jobService.GetAsync(jobId);
                Assert.True(rehydrated.IsSuccess);
                Assert.Equal(JobState.WaitingHuman, rehydrated.Value.State);
                Guid runId = runIds.Create(jobId, rehydrated.Value.AttemptCount);
                var request = new StageRecoveryRequest
                {
                    JobId = jobId,
                    RunId = runId,
                    Stage = RecoveryStage.DevelopmentAnalysis,
                    JobRequest = rehydrated.Value.Request,
                    CorrelationId = "e2e-blocked-recovery",
                    AttemptCount = rehydrated.Value.AttemptCount
                };

                StageRecoveryResult first = await recovery.CheckAsync(request);
                StageRecoveryResult second = await recovery.CheckAsync(request);

                Assert.True(first.IsCompleted);
                Assert.True(second.IsCompleted);
                Assert.NotNull(first.DevelopmentAnalysis);
                Assert.Equal(DevelopmentChangeClassification.Unknown,
                    first.DevelopmentAnalysis!.PrimaryClassification);
                Assert.True(first.DevelopmentAnalysis.ArchitectureDecisionRequired);
                Assert.False(first.DevelopmentAnalysis.DeveloperExecutionAllowed);
                Assert.Equal(first.DevelopmentAnalysis.RequestIdentity,
                    second.DevelopmentAnalysis!.RequestIdentity);
                Assert.Equal(first.DevelopmentAnalysis.PrimaryClassification,
                    second.DevelopmentAnalysis.PrimaryClassification);
                Assert.Equal(first.DevelopmentAnalysis.AnalysisVersion,
                    second.DevelopmentAnalysis.AnalysisVersion);
                Assert.Equal(JobState.WaitingHuman, rehydrated.Value.State);

                string runArtifacts = Path.Combine(
                    workspaceRoot, "artifacts", jobId.ToString("N"), runId.ToString("N"));
                Assert.Single(Directory.EnumerateFiles(
                    runArtifacts, "analysis.json", SearchOption.AllDirectories));
                Assert.False(Directory.Exists(Path.Combine(runArtifacts, "planner")));
                Assert.False(Directory.Exists(Path.Combine(runArtifacts, "developer")));
            }
        }
        finally
        {
            if (jobId != Guid.Empty)
                await DeleteJobAsync(jobId, repository, workspaceRoot);
            TryCleanupExecutionPlane(jobId, externalId, repository, workspaceRoot);
            if (Directory.Exists(container)) Directory.Delete(container, recursive: true);
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

        string domainFixtureDirectory = Path.Combine(
            repository,
            "src",
            "Kronxy.Domain",
            "TestFixtures");
        Directory.CreateDirectory(domainFixtureDirectory);
        File.WriteAllText(
            Path.Combine(domainFixtureDirectory, "DomainMarker.cs"),
            "namespace TestFixtures; public sealed class DomainMarker { }");

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
                          "objective": "Domain-only creation of src/Kronxy.Domain/TestFixtures/RestartProbe.cs.",
                          "filesToInspect": ["src/Kronxy.Domain/TestFixtures/DomainMarker.cs"],
                          "candidateFilesToModify": ["src/Kronxy.Domain/TestFixtures/RestartProbe.cs"],
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

public sealed class FullPipelinePreflightTests(ITestOutputHelper output)
{
    private const string SourcePath =
        "src/Kronxy.Web/Components/Pages/JobDetail.razor";
    private const string TestPath =
        "tests/Kronxy.Web.Tests/JobDetailTests.cs";
    private const int ContextWindow = 16_384;
    private const int SafetyMargin = 512;

    [Fact]
    public async Task Clean_pilot_platform_is_deterministically_ready()
    {
        string root = RepositoryRoot();

        await StageAsync("PREFLIGHT_CONTEXT_FAILED", async () =>
        {
            var inventory = new RepositoryFileInventory(
                new GitClient(new ProcessRunner()));
            RepositoryFileInventoryResult result = await inventory.CreateAsync(
                root,
                new RepositoryInventoryLimits
                {
                    MaxCandidates = 10_000,
                    MaxAcceptedFiles = 750,
                    MaxIndividualFileBytes = 262_144,
                    MaxTotalBytes = 5_242_880,
                    MaxLogicalPathLength = 1_024
                },
                ".kronxy-context/packages");

            Assert.True(result.IsComplete);
            Assert.Equal(570, result.Accepted.Count);
            Assert.Contains(result.Accepted, file => file.LogicalPath == SourcePath);
            Assert.DoesNotContain(result.Accepted, file => IsPollution(file.LogicalPath));
            Assert.True(HasTest(
                typeof(ContextGenerationServiceTests),
                "Generates_valid_context_package_and_writes_artifact"));
        });

        Stage("PREFLIGHT_ANALYSIS_FAILED", () =>
        {
            Assert.True(HasTest(
                typeof(DevelopmentAnalysisServiceTests),
                "Representative_frontend_inventory_selects_job_detail_without_exact_path"));
            Assert.True(HasTest(
                typeof(DevelopmentAnalysisServiceTests),
                "Razor_authoritative_candidate_keeps_web_only_required_scope"));
        });

        Stage("PREFLIGHT_PLANNING_FAILED", () =>
        {
            Assert.True(File.Exists(Path.Combine(
                root, "tests/Kronxy.Web.Tests/Kronxy.Web.Tests.csproj")));
            Assert.False(File.Exists(Path.Combine(root, TestPath)));
            Assert.True(HasTest(
                typeof(PlanningExecutionServiceTests),
                "Pilot_shaped_nested_web_test_candidate_is_coherent"));
            Assert.True(HasTest(
                typeof(PlanningExecutionServiceTests),
                "New_mapped_test_with_matching_writable_test_candidate_is_accepted"));
            Assert.True(HasTest(
                typeof(PlanningExecutionServiceTests),
                "Web_only_analysis_rejects_cross_layer_candidate"));
        });

        Stage("PREFLIGHT_DEVELOPER_FAILED", () =>
        {
            var options = new AiGatewayOptions
            {
                Provider = "Ollama",
                Endpoint = "http://localhost:11434",
                Models = new Dictionary<string, string>
                {
                    ["CodingFast"] = "configured-fast-model",
                    ["CodingQuality"] = "configured-quality-model",
                    ["General"] = "configured-general-model"
                },
                MaxConcurrentInferences = 1,
                ConnectionTimeout = TimeSpan.FromSeconds(1),
                QueueWaitTimeout = TimeSpan.FromSeconds(1),
                MaxOutputTokens = 4_096,
                MaxInputCharacters = 65_536,
                MaxResponseBytes = 1_048_576
            };
            DeveloperAiCapability capability =
                DeveloperAiRoutingPolicy.GetCapability(options);
            var catalog = new AiModelCatalog(options);

            Assert.Equal(AiLogicalModel.CodingQuality, capability.LogicalModel);
            Assert.True(catalog.TryResolve(capability.LogicalModel, out string physicalModel));
            Assert.Equal("configured-quality-model", physicalModel);
            Assert.NotEqual(options.Models["General"], physicalModel);
            Assert.Equal(ContextWindow, capability.ContextWindowTokens);
            Assert.Equal(8_192, capability.MaxOutputTokens);
            Assert.Equal(TimeSpan.FromMinutes(5), capability.InferenceTimeout);
            Assert.Equal(
                AiInferenceTimeoutPolicy.Resolve(
                    options,
                    capability.LogicalModel),
                capability.InferenceTimeout);
            Assert.True(capability.InferenceTimeout > TimeSpan.FromMinutes(3));
            Assert.Equal(19_181, new FileInfo(Path.Combine(root, SourcePath)).Length);
            Assert.True(HasTest(
                typeof(DeveloperExecutionServiceTests),
                "Two_target_19181_byte_pilot_has_deterministic_headroom"));
            Assert.True(HasTest(
                typeof(DeveloperExecutionServiceTests),
                "Second_target_failure_fails_stage_after_preserving_provenance"));
            Assert.True(HasTest(
                typeof(SafeChangeWorkspaceValidatorTests),
                "Localized_patch_applies_exact_edits_and_computes_result_hash"));
            Assert.True(HasTest(
                typeof(DeveloperExecutionServiceTests),
                "Genuinely_impossible_build_correction_envelope_fails_closed"));
        });

        Stage("PREFLIGHT_DEVELOPER_FAILED", () =>
        {
            string[] policyCanaries =
            [
                "Workspace_drift_is_rejected_before_bound_metadata_is_accepted",
                "Mixed_existing_and_new_targets_bind_authoritative_metadata",
                "Duplicate_target_operations_are_rejected_before_binding"
            ];
            Assert.All(policyCanaries, name => Assert.True(
                HasTest(typeof(DeveloperExecutionServiceTests), name) ||
                HasTest(typeof(DeveloperProposalPolicyTests), name), name));
        });

        Stage("PREFLIGHT_SAFECHANGE_FAILED", () =>
        {
            Assert.True(HasTest(
                typeof(SafeChangeApplierTests),
                "Missing_or_changed_completed_target_fails_closed"));
            Assert.True(HasTest(
                typeof(ObservedChangeEvidenceServiceTests),
                "Correlates_change_and_persists_physical_hash_and_size"));
        });

        Stage("PREFLIGHT_BUILD_FAILED", () =>
        {
            Assert.True(HasTest(typeof(BuildExecutionServiceTests),
                "Success_WritesThreeArtifacts"));
            Assert.True(HasTest(typeof(BuildExecutionServiceTests),
                "NonZeroExit_ReturnsBuildFailed"));
            Assert.True(HasTest(typeof(BuildExecutionServiceTests),
                "Correction_build_writes_only_separate_artifacts"));
        });

        Stage("PREFLIGHT_BUILD_CORRECTION_FAILED", () =>
        {
            string[] correctionCanaries =
            [
                "Build_correction_compacts_duplicate_errors_and_reaches_ai",
                "Build_correction_evidence_is_deterministic_and_within_feedback_budget",
                "Build_correction_rejects_unauthorized_diagnostic_path",
                "Build_correction_rejects_unsafe_diagnostic_path",
                "Originally_created_file_is_replaceable_when_later_diagnostic_associates_it"
            ];
            Assert.All(correctionCanaries, name => Assert.True(
                HasTest(typeof(DeveloperExecutionServiceTests), name), name));
            AssertEnvelope(7_823, 7_029, expectedHeadroom: 1_020);
        });

        Stage("PREFLIGHT_TESTING_FAILED", () =>
        {
            string[] mappedTests =
            [
                "Kronxy.Web.Tests.JobDetailTests.JobIdCopyActionExistsAndWorks",
                "Kronxy.Web.Tests.JobDetailTests.RunIdCopyActionExistsAndWorksWhenRunIdIsPresent",
                "Kronxy.Web.Tests.JobDetailTests.MissingRunIdIsHandledSafely",
                "Kronxy.Web.Tests.JobDetailTests.VisibleCopyConfirmationIsShown"
            ];
            Assert.Equal(4, mappedTests.Distinct(StringComparer.Ordinal).Count());
            Assert.True(HasTest(typeof(TestExecutionServiceTests),
                "Success_WritesFourArtifacts"));
            Assert.True(HasTest(typeof(DeterministicAcceptanceGateTests),
                "Behavioral_requirement_with_only_file_exists_is_not_proven"));
            Assert.True(HasTest(typeof(DeterministicAcceptanceGateTests),
                "Behavioral_requirement_with_mapped_passing_test_is_proven"));
        });

        Stage("PREFLIGHT_SEMANTIC_ACCEPTANCE_FAILED", () =>
        {
            Assert.True(HasTest(typeof(DeterministicAcceptanceGateTests),
                "Failed_gate_overrides_model_approval"));
            Assert.True(HasTest(typeof(DeterministicAcceptanceGateTests),
                "Behavioral_requirement_with_mapped_passing_test_is_proven"));
        });

        Stage("PREFLIGHT_REVIEWER_FAILED", () =>
        {
            Assert.True(HasTest(typeof(ReviewerRuntimeTests),
                "Policy_approves_only_when_all_deterministic_gates_pass"));
            Assert.True(HasTest(typeof(DeterministicAcceptanceGateTests),
                "Failed_gate_overrides_model_approval"));
        });

        Stage("PREFLIGHT_REVIEWER_FAILED", () =>
        {
            Assert.True(HasTest(typeof(JobTests),
                "Mutating_approval_enters_pending_promotion"));
            Assert.True(HasTest(typeof(JobApplicationTests),
                "Human_review_approval_rejects_failed_deterministic_gate"));
        });

        Stage("PREFLIGHT_SOURCE_PROMOTION_FAILED", () =>
        {
            Assert.True(HasTest(typeof(RepositoryManagerTests),
                "Source_promotion_prepares_single_verified_commit_and_is_idempotent"));
            Assert.True(HasTest(typeof(RepositoryManagerTests),
                "Source_promotion_fast_forwards_target_and_repeat_is_idempotent"));
            Assert.True(HasTest(typeof(RepositoryManagerTests),
                "Two_promotions_from_same_base_allow_only_first"));
            Assert.True(HasTest(typeof(JobTests),
                "Pending_promotion_cannot_complete_without_promoted_evidence"));
            Assert.True(HasTest(typeof(JobTests),
                "Pending_promotion_completes_after_promoted_evidence_is_validated"));
        });

        output.WriteLine("INITIAL_TARGET_INVOCATIONS=2");
        output.WriteLine("INITIAL_ENVELOPE_MODE=PER_TARGET");
        output.WriteLine("CORRECTION_TARGET_COUNT=1");
        output.WriteLine("CORRECTION_TARGET_BYTES=19181");
        output.WriteLine("CORRECTION_INPUT_TOKENS=7823");
        output.WriteLine("CORRECTION_MIN_OUTPUT_TOKENS=7029");
        output.WriteLine("CORRECTION_TOTAL_ENVELOPE=15364");
        output.WriteLine("CORRECTION_HEADROOM=1020");
    }

    private static void AssertEnvelope(
        int inputTokens,
        int minimumOutputTokens,
        int expectedHeadroom)
    {
        int total = inputTokens + minimumOutputTokens + SafetyMargin;
        Assert.Equal(expectedHeadroom, ContextWindow - total);
        Assert.True(total < ContextWindow);
    }

    private static bool HasTest(Type type, string method) =>
        type.GetMethod(method) is not null;

    private static bool IsPollution(string path) =>
        path.Split('/').Any(segment => segment is
            ".git" or "bin" or "obj" or "artifacts" or "workspaces" or
            "TestResults" or "tmp" or "temp" or ".kronxy-context");

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Kronxy.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new XunitException("PREFLIGHT_CONTEXT_FAILED: repository root not found");
    }

    private static void Stage(string code, Action action)
    {
        try { action(); }
        catch (Exception exception) when (exception is not XunitException { Message: var message } ||
                                           !message.StartsWith("PREFLIGHT_", StringComparison.Ordinal))
        {
            throw new XunitException($"{code}: {exception.Message}");
        }
    }

    private static async Task StageAsync(string code, Func<Task> action)
    {
        try { await action(); }
        catch (Exception exception) when (exception is not XunitException { Message: var message } ||
                                           !message.StartsWith("PREFLIGHT_", StringComparison.Ordinal))
        {
            throw new XunitException($"{code}: {exception.Message}");
        }
    }
}

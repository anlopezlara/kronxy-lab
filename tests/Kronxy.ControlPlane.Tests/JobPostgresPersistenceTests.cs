using System.Globalization;
using Kronxy.Application;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Exceptions;
using Kronxy.Application.Jobs;
using Kronxy.Domain.Jobs;
using Kronxy.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

[CollectionDefinition(
    "PostgresControlPlane",
    DisableParallelization = true)]
public sealed class PostgresControlPlaneCollection
{
}

[Collection("PostgresControlPlane")]
public sealed class JobPostgresPersistenceTests
{

    private static readonly string ExecutionPlaneTestRoot =
        Path.Combine(
            Path.GetTempPath(),
            "kronxy-postgres-di-tests");

    private static readonly string ExecutionPlaneRepository =
        Path.Combine(
            ExecutionPlaneTestRoot,
            "repository");

    private static readonly string ExecutionPlaneWorkspaces =
        Path.Combine(
            ExecutionPlaneTestRoot,
            "workspaces");

    private static readonly object ExecutionPlaneSync =
        new();


    private static string ConnectionString =>
        Environment.GetEnvironmentVariable(
            "ConnectionStrings__Database")
        ?? throw new InvalidOperationException(
            "ConnectionStrings__Database is required " +
            "for PostgreSQL integration tests.");

    [Fact]
    public async Task Job_is_persisted_and_rehydrated_with_transition_using_new_dbcontext()
    {
        var externalId =
            $"IT-{Guid.NewGuid():N}";

        Guid jobId = Guid.Empty;

        try
        {
            await using (var provider = BuildProvider())
            {
                await using var scope =
                    provider.CreateAsyncScope();

                var repository =
                    scope.ServiceProvider
                        .GetRequiredService<IJobRepository>();

                var unitOfWork =
                    scope.ServiceProvider
                        .GetRequiredService<
                            Kronxy.Domain.Abstractions
                                .IUnitOfWork>();

                var limitsResult =
                    JobLimits.Create(
                        TimeSpan.FromMinutes(30),
                        3,
                        10,
                        20);

                Assert.True(limitsResult.IsSuccess);

                var now = DateTime.UtcNow;

                var createResult =
                    Job.Create(
                        Guid.NewGuid(),
                        externalId,
                        "integration recovery test",
                        limitsResult.Value,
                        now);

                Assert.True(createResult.IsSuccess);

                var job = createResult.Value;
                jobId = job.Id;

                var transitionResult =
                    job.TransitionTo(
                        JobState.ContextBuilding,
                        now.AddSeconds(1),
                        "Integration transition.",
                        "integration-test",
                        Guid.NewGuid().ToString("N"));

                Assert.True(
                    transitionResult.IsSuccess);

                repository.Add(job);

                await unitOfWork.SaveChangesAsync();
            }

            await using (var provider = BuildProvider())
            {
                await using var scope =
                    provider.CreateAsyncScope();

                var repository =
                    scope.ServiceProvider
                        .GetRequiredService<IJobRepository>();

                var recovered =
                    await repository
                        .GetByExternalIdAsync(externalId);

                Assert.NotNull(recovered);
                Assert.Equal(jobId, recovered.Id);
                Assert.Equal(
                    externalId,
                    recovered.ExternalId);
                Assert.Equal(
                    "integration recovery test",
                    recovered.Request);
                Assert.Equal(
                    JobState.ContextBuilding,
                    recovered.State);
                Assert.Null(recovered.ResumeState);
                Assert.Equal(1, recovered.AttemptCount);
                Assert.Single(recovered.Transitions);
                Assert.Equal(
                    JobState.Created,
                    recovered.Transitions[0].FromState);
                Assert.Equal(
                    JobState.ContextBuilding,
                    recovered.Transitions[0].ToState);
                Assert.True(recovered.Version >= 1);
                Assert.Equal(
                    TimeSpan.FromMinutes(30),
                    recovered.Limits.MaxJobDuration);
                Assert.Equal(
                    3,
                    recovered.Limits.MaxAttempts);
                Assert.Equal(
                    10,
                    recovered.Limits.MaxAgentIterations);
                Assert.Equal(
                    20,
                    recovered.Limits.MaxAiCalls);
            }
        }
        finally
        {
            if (jobId != Guid.Empty)
            {
                await DeleteJobAsync(jobId);
            }
        }
    }

    [Fact]
    public async Task Production_generator_uses_persistent_KRX_sequence_across_providers()
    {
        string first;
        string second;

        await using (var provider = BuildProvider())
        {
            var generator =
                provider.GetRequiredService<
                    IJobIdGenerator>();

            Assert.Equal(
                "PostgresJobIdGenerator",
                generator.GetType().Name);

            first = generator.NewExternalId();
        }

        await using (var provider = BuildProvider())
        {
            var generator =
                provider.GetRequiredService<
                    IJobIdGenerator>();

            second = generator.NewExternalId();
        }

        Assert.Matches(
            "^KRX-[0-9]{6,}$",
            first);

        Assert.Matches(
            "^KRX-[0-9]{6,}$",
            second);

        var firstNumber =
            ParseExternalId(first);

        var secondNumber =
            ParseExternalId(second);

        Assert.True(
            secondNumber > firstNumber);
    }

    [Fact]
    public async Task Production_generator_is_unique_under_concurrency()
    {
        const int count = 24;

        await using var provider =
            BuildProvider();

        var generator =
            provider.GetRequiredService<
                IJobIdGenerator>();

        var tasks =
            Enumerable
                .Range(0, count)
                .Select(
                    _ => Task.Run(
                        () => generator.NewExternalId()))
                .ToArray();

        var ids = await Task.WhenAll(tasks);

        Assert.Equal(count, ids.Length);
        Assert.Equal(
            count,
            ids.Distinct(
                StringComparer.Ordinal).Count());

        Assert.All(
            ids,
            id => Assert.Matches(
                "^KRX-[0-9]{6,}$",
                id));
    }

    [Fact]
    public async Task Two_dbcontexts_detect_optimistic_concurrency_conflict()
    {
        var externalId =
            $"IT-CONC-{Guid.NewGuid():N}";

        Guid jobId = Guid.Empty;

        try
        {
            await using (var provider = BuildProvider())
            {
                await using var scope =
                    provider.CreateAsyncScope();

                var repository =
                    scope.ServiceProvider
                        .GetRequiredService<IJobRepository>();

                var unitOfWork =
                    scope.ServiceProvider
                        .GetRequiredService<
                            Kronxy.Domain.Abstractions
                                .IUnitOfWork>();

                var limits =
                    JobLimits.Create(
                        TimeSpan.FromMinutes(30),
                        3,
                        10,
                        20);

                Assert.True(limits.IsSuccess);

                var result =
                    Job.Create(
                        Guid.NewGuid(),
                        externalId,
                        "integration concurrency test",
                        limits.Value,
                        DateTime.UtcNow);

                Assert.True(result.IsSuccess);

                jobId = result.Value.Id;

                repository.Add(result.Value);

                await unitOfWork.SaveChangesAsync();
            }

            await using var provider1 =
                BuildProvider();

            await using var provider2 =
                BuildProvider();

            await using var scope1 =
                provider1.CreateAsyncScope();

            await using var scope2 =
                provider2.CreateAsyncScope();

            var repository1 =
                scope1.ServiceProvider
                    .GetRequiredService<IJobRepository>();

            var repository2 =
                scope2.ServiceProvider
                    .GetRequiredService<IJobRepository>();

            var unitOfWork1 =
                scope1.ServiceProvider
                    .GetRequiredService<
                        Kronxy.Domain.Abstractions
                            .IUnitOfWork>();

            var unitOfWork2 =
                scope2.ServiceProvider
                    .GetRequiredService<
                        Kronxy.Domain.Abstractions
                            .IUnitOfWork>();

            var job1 =
                await repository1.GetByIdAsync(jobId);

            var job2 =
                await repository2.GetByIdAsync(jobId);

            Assert.NotNull(job1);
            Assert.NotNull(job2);
            Assert.Equal(job1.Version, job2.Version);

            var now = DateTime.UtcNow;

            var transition1 =
                job1.TransitionTo(
                    JobState.ContextBuilding,
                    now,
                    "First writer.",
                    "integration-test",
                    Guid.NewGuid().ToString("N"));

            var transition2 =
                job2.TransitionTo(
                    JobState.ContextBuilding,
                    now.AddMilliseconds(1),
                    "Second writer.",
                    "integration-test",
                    Guid.NewGuid().ToString("N"));

            Assert.True(transition1.IsSuccess);
            Assert.True(transition2.IsSuccess);

            await unitOfWork1.SaveChangesAsync();

            await Assert.ThrowsAsync<
                ConcurrencyException>(
                async () =>
                    await unitOfWork2
                        .SaveChangesAsync());
        }
        finally
        {
            if (jobId != Guid.Empty)
            {
                await DeleteJobAsync(jobId);
            }
        }
    }

    [Fact]
    public async Task Artifact_metadata_is_persisted_recovered_and_idempotent()
    {
        Guid artifactId = Guid.NewGuid();
        Guid jobId = Guid.NewGuid();
        Guid runId = Guid.NewGuid();

        var now =
            DateTimeOffset.UtcNow;

        var createdAtUtc =
            new DateTimeOffset(
                now.Ticks -
                    now.Ticks % 10,
                now.Offset);

        var artifact =
            new ArtifactRecord
            {
                ArtifactId = artifactId,
                JobId = jobId,
                RunId = runId,
                ArtifactType =
                    ArtifactType.ContextPackage,
                RelativePath =
                    $"{jobId:N}/{runId:N}/context/context.zip",
                Sha256 =
                    new string('a', 64),
                SizeBytes = 4096,
                CreatedAtUtc = createdAtUtc,
                CorrelationId =
                    $"it-{Guid.NewGuid():N}"
            };

        try
        {
            // First process/scope writes metadata.
            await using (var provider = BuildProvider())
            {
                await using var scope =
                    provider.CreateAsyncScope();

                var repository =
                    scope.ServiceProvider
                        .GetRequiredService<
                            IArtifactMetadataRepository>();

                await repository.AddAsync(
                    artifact);
            }

            // A completely new provider/scope must recover it.
            await using (var provider = BuildProvider())
            {
                await using var scope =
                    provider.CreateAsyncScope();

                var repository =
                    scope.ServiceProvider
                        .GetRequiredService<
                            IArtifactMetadataRepository>();

                ArtifactRecord? recovered =
                    await repository.GetByIdAsync(
                        artifactId);

                Assert.NotNull(recovered);

                Assert.Equal(
                    artifact.ArtifactId,
                    recovered.ArtifactId);

                Assert.Equal(
                    artifact.JobId,
                    recovered.JobId);

                Assert.Equal(
                    artifact.RunId,
                    recovered.RunId);

                Assert.Equal(
                    artifact.ArtifactType,
                    recovered.ArtifactType);

                Assert.Equal(
                    artifact.RelativePath,
                    recovered.RelativePath);

                Assert.Equal(
                    artifact.Sha256,
                    recovered.Sha256);

                Assert.Equal(
                    artifact.SizeBytes,
                    recovered.SizeBytes);

                Assert.Equal(
                    artifact.CorrelationId,
                    recovered.CorrelationId);

                Assert.Equal(
                    artifact.CreatedAtUtc,
                    recovered.CreatedAtUtc);

                IReadOnlyList<ArtifactRecord>
                    byJobAndRun =
                        await repository
                            .GetByJobAndRunAsync(
                                jobId,
                                runId);

                ArtifactRecord single =
                    Assert.Single(
                        byJobAndRun);

                Assert.Equal(
                    artifactId,
                    single.ArtifactId);
            }

            // Repeating the exact same metadata must be idempotent.
            await using (var provider = BuildProvider())
            {
                await using var scope =
                    provider.CreateAsyncScope();

                var repository =
                    scope.ServiceProvider
                        .GetRequiredService<
                            IArtifactMetadataRepository>();

                await repository.AddAsync(
                    artifact);
            }

            // Verify physical PostgreSQL row count remains exactly one.
            await using (var provider = BuildProvider())
            {
                await using var scope =
                    provider.CreateAsyncScope();

                var dbContext =
                    scope.ServiceProvider
                        .GetRequiredService<
                            ApplicationDbContext>();

                int physicalCount =
                    await dbContext
                        .Set<
                            Kronxy.Infrastructure
                                .Artifacts.Persistence
                                .ArtifactMetadataEntity>()
                        .AsNoTracking()
                        .CountAsync(
                            item =>
                                item.ArtifactId ==
                                    artifactId);

                Assert.Equal(
                    1,
                    physicalCount);

                var repository =
                    scope.ServiceProvider
                        .GetRequiredService<
                            IArtifactMetadataRepository>();

                IReadOnlyList<ArtifactRecord>
                    finalRecords =
                        await repository
                            .GetByJobAndRunAsync(
                                jobId,
                                runId);

                Assert.Single(
                    finalRecords);
            }
        }
        finally
        {
            await using var provider =
                BuildProvider();

            await using var scope =
                provider.CreateAsyncScope();

            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<
                        ApplicationDbContext>();

            await dbContext.Database
                .ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM job_artifacts WHERE artifact_id = {artifactId}");
        }
    }


    [Fact]
    public async Task Artifact_metadata_conflicts_fail_closed()
    {
        Guid artifactId = Guid.NewGuid();
        Guid jobId = Guid.NewGuid();
        Guid runId = Guid.NewGuid();

        var now =
            DateTimeOffset.UtcNow;

        var createdAtUtc =
            new DateTimeOffset(
                now.Ticks -
                    now.Ticks % 10,
                now.Offset);

        string relativePath =
            $"{jobId:N}/{runId:N}/context/context.zip";

        var original =
            new ArtifactRecord
            {
                ArtifactId = artifactId,
                JobId = jobId,
                RunId = runId,
                ArtifactType =
                    ArtifactType.ContextPackage,
                RelativePath = relativePath,
                Sha256 = new string('a', 64),
                SizeBytes = 4096,
                CreatedAtUtc = createdAtUtc,
                CorrelationId =
                    $"it-{Guid.NewGuid():N}"
            };

        try
        {
            await using (var provider = BuildProvider())
            {
                await using var scope =
                    provider.CreateAsyncScope();

                var repository =
                    scope.ServiceProvider
                        .GetRequiredService<
                            IArtifactMetadataRepository>();

                await repository.AddAsync(
                    original);
            }

            // Exact retry remains idempotent.
            await using (var provider = BuildProvider())
            {
                await using var scope =
                    provider.CreateAsyncScope();

                var repository =
                    scope.ServiceProvider
                        .GetRequiredService<
                            IArtifactMetadataRepository>();

                await repository.AddAsync(
                    original);
            }

            // Same ArtifactId but different SHA must fail closed.
            var changedHash =
                original with
                {
                    Sha256 =
                        new string('b', 64)
                };

            await using (var provider = BuildProvider())
            {
                await using var scope =
                    provider.CreateAsyncScope();

                var repository =
                    scope.ServiceProvider
                        .GetRequiredService<
                            IArtifactMetadataRepository>();

                await Assert.ThrowsAsync<
                    InvalidOperationException>(
                    () =>
                        repository.AddAsync(
                            changedHash));
            }

            // Same physical path with a different identity must fail.
            var changedIdentity =
                original with
                {
                    ArtifactId =
                        Guid.NewGuid()
                };

            await using (var provider = BuildProvider())
            {
                await using var scope =
                    provider.CreateAsyncScope();

                var repository =
                    scope.ServiceProvider
                        .GetRequiredService<
                            IArtifactMetadataRepository>();

                await Assert.ThrowsAsync<
                    InvalidOperationException>(
                    () =>
                        repository.AddAsync(
                            changedIdentity));
            }

            // No conflicting row may have been persisted.
            await using (var provider = BuildProvider())
            {
                await using var scope =
                    provider.CreateAsyncScope();

                var repository =
                    scope.ServiceProvider
                        .GetRequiredService<
                            IArtifactMetadataRepository>();

                IReadOnlyList<ArtifactRecord> records =
                    await repository
                        .GetByJobAndRunAsync(
                            jobId,
                            runId);

                ArtifactRecord persisted =
                    Assert.Single(
                        records);

                Assert.Equal(
                    original.ArtifactId,
                    persisted.ArtifactId);

                Assert.Equal(
                    original.Sha256,
                    persisted.Sha256);

                Assert.Equal(
                    original.SizeBytes,
                    persisted.SizeBytes);
            }
        }
        finally
        {
            await using var provider =
                BuildProvider();

            await using var scope =
                provider.CreateAsyncScope();

            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<
                        ApplicationDbContext>();

            await dbContext.Database
                .ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM job_artifacts WHERE artifact_id = {artifactId}");
        }
    }


    private static ServiceProvider BuildProvider()
    {
        EnsureExecutionPlaneTestRepository();

        string artifactStoreRoot =
            Path.Combine(
                ExecutionPlaneTestRoot,
                "artifacts");

        Directory.CreateDirectory(
            artifactStoreRoot);

        var configuration =
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
                            ExecutionPlaneRepository,

                        ["ExecutionPlane:WorkspaceRoot"] =
                            ExecutionPlaneWorkspaces,

                        ["ExecutionPlane:DotnetExecutable"] =
                            TestToolResolver.Dotnet(),

                        ["ExecutionPlane:GitExecutable"] =
                            TestToolResolver.Git(),

                        ["ExecutionPlane:DotnetTarget"] =
                            "Kronxy.sln",

                        ["ArtifactStore:RootPath"] =
                            artifactStoreRoot,

                        ["ArtifactStore:MaxArtifactBytes"] =
                            "16777216"
                    })
                .Build();

        var services =
            new ServiceCollection();

        services.AddApplication();
        services.AddInfrastructure(
            configuration);

        return services
            .BuildServiceProvider();
    }

    private static void EnsureExecutionPlaneTestRepository()
    {
        lock (ExecutionPlaneSync)
        {
            if (Directory.Exists(
                    Path.Combine(
                        ExecutionPlaneRepository,
                        ".git")))
            {
                Directory.CreateDirectory(
                    ExecutionPlaneWorkspaces);

                return;
            }

            Directory.CreateDirectory(
                ExecutionPlaneTestRoot);

            Directory.CreateDirectory(
                ExecutionPlaneRepository);

            Directory.CreateDirectory(
                ExecutionPlaneWorkspaces);

            RunGit(
                ExecutionPlaneRepository,
                "init",
                "-b",
                "main");

            RunGit(
                ExecutionPlaneRepository,
                "config",
                "user.name",
                "KRONXY Tests");

            RunGit(
                ExecutionPlaneRepository,
                "config",
                "user.email",
                "kronxy@example.invalid");

            File.WriteAllText(
                Path.Combine(
                    ExecutionPlaneRepository,
                    "tracked.txt"),
                "integration");

            RunGit(
                ExecutionPlaneRepository,
                "add",
                "--all",
                "--");

            RunGit(
                ExecutionPlaneRepository,
                "commit",
                "-m",
                "integration fixture");
        }
    }

    private static string RunGit(
        string workingDirectory,
        params string[] arguments)
    {
        var startInfo =
            new System.Diagnostics.ProcessStartInfo
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

        foreach (string argument
                 in arguments)
        {
            startInfo.ArgumentList.Add(
                argument);
        }

        using var process =
            System.Diagnostics.Process.Start(
                startInfo)
            ?? throw new InvalidOperationException(
                "Unable to start git test fixture.");

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
                $"Git test fixture failed: {stderr}");
        }

        return stdout;
    }

    private static long ParseExternalId(
        string externalId)
    {
        return long.Parse(
            externalId.AsSpan(4),
            NumberStyles.None,
            CultureInfo.InvariantCulture);
    }

    private static async Task DeleteJobAsync(
        Guid jobId)
    {
        await using var provider =
            BuildProvider();

        await using var scope =
            provider.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<
                    ApplicationDbContext>();

        await dbContext.Database
            .ExecuteSqlInterpolatedAsync(
                $"DELETE FROM jobs WHERE id = {jobId}");
    }
}

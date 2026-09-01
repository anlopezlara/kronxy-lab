using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Data;
using Kronxy.Application.Abstractions.Email;
using Kronxy.Application.Jobs;
using Kronxy.Application.Execution;
using Kronxy.Application.Artifacts;
using Kronxy.Application.AI;
using Kronxy.Infrastructure.AI;
using Kronxy.Infrastructure.AI.Ollama;
using Kronxy.Application.Repositories;
using Kronxy.Application.Workspaces;
using Kronxy.Infrastructure.Execution;
using Kronxy.Infrastructure.Artifacts;
using Kronxy.Infrastructure.Git;
using Kronxy.Infrastructure.Workspaces;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Apartments;
using Kronxy.Domain.Bookings;
using Kronxy.Domain.Users;
using Kronxy.Domain.Catalogs;
using Kronxy.Infrastructure.Clock;
using Kronxy.Infrastructure.Data;
using Kronxy.Infrastructure.Email;
using Kronxy.Infrastructure.Repositories;
using Kronxy.Domain.Projects;
using Kronxy.Domain.ProjectTasks;
using Kronxy.Domain.Jobs;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Kronxy.Infrastructure.Artifacts.Persistence;
using Kronxy.Application.Context;
using Kronxy.Infrastructure.Context;
using Kronxy.Context.Configuration;

namespace Kronxy.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddTransient<IDateTimeProvider, DateTimeProvider>();

        services.AddTransient<IEmailService, EmailService>();

        var connectionString =
            configuration.GetConnectionString("Database") ??
            throw new ArgumentNullException(nameof(configuration));

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
        });

        //------------ SERVICES------------------------------------------

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();

        services.AddScoped<IApartmentRepository, ApartmentRepository>();

        services.AddScoped<IBookingRepository, BookingRepository>();

        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectStatusRepository, ProjectStatusRepository>();
        services.AddScoped<IProjectTypeRepository, ProjectTypeRepository>();
        services.AddScoped<IProjectPriorityRepository, ProjectPriorityRepository>();

        services.AddScoped<IProjectTaskRepository, ProjectTaskRepository>();

        services.AddScoped<ICatalogRepository, CatalogRepository>();

        services.AddScoped<IJobRepository, JobRepository>();

        services.AddScoped<
            IArtifactMetadataRepository,
            ArtifactMetadataRepository>();

        //---------------------------------------------------------------

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddSingleton<ISqlConnectionFactory>(_ =>
            new SqlConnectionFactory(connectionString));

        services.AddSingleton<IJobIdGenerator, PostgresJobIdGenerator>();

        ExecutionPlaneOptions executionOptions =
            new()
            {
                RepositoryRoot =
                    configuration[
                        "ExecutionPlane:RepositoryRoot"]
                    ?? string.Empty,

                WorkspaceRoot =
                    configuration[
                        "ExecutionPlane:WorkspaceRoot"]
                    ?? string.Empty,

                DotnetExecutable =
                    configuration[
                        "ExecutionPlane:DotnetExecutable"]
                    ?? string.Empty,

                GitExecutable =
                    configuration[
                        "ExecutionPlane:GitExecutable"]
                    ?? string.Empty,

                DotnetTarget =
                    configuration[
                        "ExecutionPlane:DotnetTarget"]
                    ?? string.Empty
            };

        executionOptions.Validate();

        services.AddSingleton(
            executionOptions);

        services.AddSingleton<
            IExecutionTargetProvider,
            ConfiguredExecutionTargetProvider>();

        services.AddSingleton<IWorkspaceManager>(
            _ =>
                new WorkspaceManager(
                    executionOptions.WorkspaceRoot));

        services.AddSingleton<IRepositoryManager>(
            _ =>
                new RepositoryManager(
                    executionOptions.RepositoryRoot,
                    executionOptions.WorkspaceRoot,
                    executionOptions.GitExecutable));

        services.AddSingleton<ISourceRevisionProvider>(
            _ =>
                new GitSourceRevisionProvider(
                    executionOptions.RepositoryRoot,
                    executionOptions.GitExecutable));

        services.AddSingleton<
            IExecutionPlaneLifecycle,
            ExecutionPlaneLifecycle>();

        // KRONXY Artifact Store
        ArtifactStoreOptions artifactOptions =
            new()
            {
                RootPath =
                    configuration[
                        "ArtifactStore:RootPath"]
                    ?? string.Empty,

                MaxArtifactBytes =
                    GetRequiredPositiveLong(
                        configuration,
                        "ArtifactStore:MaxArtifactBytes")
            };

        artifactOptions.Validate();

        services.AddSingleton(
            artifactOptions);

        services.AddSingleton<
            FileSystemArtifactStore>();

        services.AddScoped<
            IArtifactStore,
            PersistingArtifactStore>();

        services.AddScoped<
            IArtifactReader,
            FileSystemArtifactReader>();

        ContextOptions defaultContextOptions =
            new();

        ContextOptions contextOptions =
            new()
            {
                MaxTextFileBytes =
                    GetOptionalPositiveInt(
                        configuration,
                        "Context:MaxTextFileBytes",
                        defaultContextOptions.MaxTextFileBytes),

                MaxLinesPerFile =
                    GetOptionalPositiveInt(
                        configuration,
                        "Context:MaxLinesPerFile",
                        defaultContextOptions.MaxLinesPerFile),

                MaxPatchBytes =
                    GetOptionalPositiveInt(
                        configuration,
                        "Context:MaxPatchBytes",
                        defaultContextOptions.MaxPatchBytes),

                TargetHandoffPackageBytes =
                    GetOptionalPositiveInt(
                        configuration,
                        "Context:TargetHandoffPackageBytes",
                        defaultContextOptions.TargetHandoffPackageBytes),

                MaxHandoffPackageBytes =
                    GetOptionalPositiveInt(
                        configuration,
                        "Context:MaxHandoffPackageBytes",
                        defaultContextOptions.MaxHandoffPackageBytes),

                MaxBaselinePackageBytes =
                    GetOptionalPositiveInt(
                        configuration,
                        "Context:MaxBaselinePackageBytes",
                        defaultContextOptions.MaxBaselinePackageBytes),

                MaxFilesPerPackage =
                    GetOptionalPositiveInt(
                        configuration,
                        "Context:MaxFilesPerPackage",
                        defaultContextOptions.MaxFilesPerPackage),

                MaxRepositoryCandidates =
                    GetOptionalPositiveInt(
                        configuration,
                        "Context:MaxRepositoryCandidates",
                        defaultContextOptions.MaxRepositoryCandidates),

                MaxLogicalPathLength =
                    GetOptionalPositiveInt(
                        configuration,
                        "Context:MaxLogicalPathLength",
                        defaultContextOptions.MaxLogicalPathLength),

                RelationshipDepth =
                    GetOptionalNonNegativeInt(
                        configuration,
                        "Context:RelationshipDepth",
                        defaultContextOptions.RelationshipDepth),

                DefaultOutputDirectory =
                    configuration[
                        "Context:DefaultOutputDirectory"]
                    ?? defaultContextOptions.DefaultOutputDirectory,

                RedactedValue =
                    configuration[
                        "Context:RedactedValue"]
                    ?? defaultContextOptions.RedactedValue
            };

        IReadOnlyList<string> contextValidation =
            contextOptions.Validate();

        if (contextValidation.Count != 0)
        {
            throw new InvalidOperationException(
                "ContextOptions default configuration is invalid.");
        }

        services.AddSingleton(
            contextOptions);

        services.AddScoped<
            IContextGenerationService,
            ContextGenerationService>();

        services.AddSingleton<
            IContextAiInputBuilder,
            ContextAiInputBuilder>();

        services.AddScoped<
            IRestoreExecutionService,
            RestoreExecutionService>();

        services.AddScoped<
            IBuildExecutionService,
            BuildExecutionService>();

        services.AddScoped<
            ITestExecutionService,
            TestExecutionService>();

        services.AddScoped<
            IPlanningExecutionService,
            PlanningExecutionService>();

        services.AddScoped<
            IStageRecoveryEvidenceService,
            StageRecoveryEvidenceService>();

        // KRONXY AI Gateway
        AiGatewayOptions aiOptions =
            new()
            {
                Provider =
                    configuration["AI:Provider"]
                    ?? string.Empty,

                Endpoint =
                    configuration["AI:Endpoint"]
                    ?? string.Empty,

                Models =
                    new Dictionary<string, string>
                    {
                        ["CodingFast"] =
                            configuration[
                                "AI:Models:CodingFast"]
                            ?? string.Empty,

                        ["CodingQuality"] =
                            configuration[
                                "AI:Models:CodingQuality"]
                            ?? string.Empty,

                        ["General"] =
                            configuration[
                                "AI:Models:General"]
                            ?? string.Empty
                    },

                MaxConcurrentInferences =
                    GetRequiredPositiveInt(
                        configuration,
                        "AI:MaxConcurrentInferences"),

                ConnectionTimeout =
                    GetRequiredPositiveTimeSpan(
                        configuration,
                        "AI:ConnectionTimeout"),

                InferenceTimeout =
                    GetRequiredPositiveTimeSpan(
                        configuration,
                        "AI:InferenceTimeout"),

                QueueWaitTimeout =
                    GetRequiredPositiveTimeSpan(
                        configuration,
                        "AI:QueueWaitTimeout"),

                MaxOutputTokens =
                    GetRequiredPositiveInt(
                        configuration,
                        "AI:MaxOutputTokens"),

                MaxInputCharacters =
                    GetRequiredPositiveInt(
                        configuration,
                        "AI:MaxInputCharacters"),

                MaxResponseBytes =
                    GetRequiredPositiveInt(
                        configuration,
                        "AI:MaxResponseBytes")
            };

        aiOptions.Validate();

        services.AddSingleton(
            aiOptions);

        services.AddSingleton<
            AiModelCatalog>();

        services.AddSingleton<
            AiStructuredOutputValidator>();

        services.AddSingleton<
            HttpClient>(
            _ =>
            {
                var handler =
                    new SocketsHttpHandler
                    {
                        ConnectTimeout =
                            aiOptions.ConnectionTimeout,

                        PooledConnectionLifetime =
                            TimeSpan.FromMinutes(5)
                    };

                return new HttpClient(
                    handler,
                    disposeHandler: true)
                {
                    BaseAddress =
                        new Uri(
                            aiOptions.Endpoint
                                .TrimEnd('/') + "/"),

                    Timeout =
                        Timeout.InfiniteTimeSpan
                };
            });

        services.AddSingleton<
            OllamaProvider>();

        services.AddSingleton<
            IAiProvider>(
            serviceProvider =>
                serviceProvider
                    .GetRequiredService<
                        OllamaProvider>());

        services.AddSingleton<
            IAiGateway,
            AiGateway>();

        services.AddSingleton<ISecureToolExecutor>(
            _ =>
                new SecureToolExecutor(
                    SecureToolExecutorOptions.Create(
                        executionOptions.DotnetExecutable,
                        executionOptions.GitExecutable)));

        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

        return services;
    }

    private static int GetOptionalPositiveInt(
        IConfiguration configuration,
        string key,
        int defaultValue)
    {
        string? raw =
            configuration[key];

        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (!int.TryParse(
                raw,
                out int value) ||
            value <= 0)
        {
            throw new InvalidOperationException(
                $"Configuration '{key}' must be a positive integer.");
        }

        return value;
    }

    private static int GetOptionalNonNegativeInt(
        IConfiguration configuration,
        string key,
        int defaultValue)
    {
        string? raw =
            configuration[key];

        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (!int.TryParse(
                raw,
                out int value) ||
            value < 0)
        {
            throw new InvalidOperationException(
                $"Configuration '{key}' must be a non-negative integer.");
        }

        return value;
    }

    private static int GetRequiredPositiveInt(
        IConfiguration configuration,
        string key)
    {
        string? raw =
            configuration[key];

        if (!int.TryParse(
                raw,
                out int value) ||
            value <= 0)
        {
            throw new InvalidOperationException(
                $"Configuration '{key}' must be a positive integer.");
        }

        return value;
    }

    private static long GetRequiredPositiveLong(
        IConfiguration configuration,
        string key)
    {
        string? raw =
            configuration[key];

        if (!long.TryParse(
                raw,
                out long value) ||
            value <= 0)
        {
            throw new InvalidOperationException(
                $"Configuration '{key}' must be a positive integer.");
        }

        return value;
    }

    private static TimeSpan GetRequiredPositiveTimeSpan(
        IConfiguration configuration,
        string key)
    {
        string? raw =
            configuration[key];

        if (!TimeSpan.TryParse(
                raw,
                out TimeSpan value) ||
            value <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                $"Configuration '{key}' must be a positive TimeSpan.");
        }

        return value;
    }
}

using Kronxy.Application.Execution;
using Kronxy.Infrastructure;
using Kronxy.Infrastructure.AI;
using Kronxy.Infrastructure.Execution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class DeveloperProposalPolicyRegistrationTests
{
    [Fact]
    public void Add_infrastructure_registers_configured_policy()
    {
        using TemporaryPaths paths =
            new();

        IConfiguration configuration =
            CreateConfiguration(
                paths,
                maxOperations: "7",
                maxCreatedFiles: "3");

        var services =
            new ServiceCollection();

        services.AddInfrastructure(
            configuration);

        using ServiceProvider provider =
            services.BuildServiceProvider();

        DeveloperChangePolicyOptions options =
            provider.GetRequiredService<
                DeveloperChangePolicyOptions>();

        AiGatewayOptions aiOptions =
            provider.GetRequiredService<
                AiGatewayOptions>();

        IDeveloperProposalPolicy policy =
            provider.GetRequiredService<
                IDeveloperProposalPolicy>();

        SafeChangeWorkspaceValidator validator =
            provider.GetRequiredService<
                SafeChangeWorkspaceValidator>();

        ISafeChangeCommitObserver observer =
            provider.GetRequiredService<
                ISafeChangeCommitObserver>();

        ISafeChangeApplier applier =
            provider.GetRequiredService<
                ISafeChangeApplier>();

        ISafeChangeApplier secondApplier =
            provider.GetRequiredService<
                ISafeChangeApplier>();


        Assert.Equal(7, options.MaxOperations);
        Assert.Equal(3, options.MaxCreatedFiles);
        Assert.Equal(1024, options.MaxFileBytes);
        Assert.Equal(
            2048,
            options.MaxTotalChangeBytes);
        Assert.Equal(
            4096,
            options.MaxProposalBytes);
        Assert.Equal(
            TimeSpan.FromMinutes(3),
            aiOptions.PlanningInferenceTimeout);
        Assert.Equal(
            TimeSpan.FromMinutes(1),
            aiOptions.DeveloperInferenceTimeout);
        Assert.IsType<
            DeveloperProposalPolicy>(
                policy);

        Assert.IsType<
            SafeChangeWorkspaceValidator>(
                validator);

        Assert.IsType<
            NoOpSafeChangeCommitObserver>(
                observer);

        Assert.IsType<
            SafeChangeApplier>(
                applier);

        Assert.Same(
            applier,
            secondApplier);

        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType ==
                    typeof(IDeveloperExecutionService) &&
                descriptor.ImplementationType ==
                    typeof(DeveloperExecutionService) &&
                descriptor.Lifetime ==
                    ServiceLifetime.Scoped);
    }

    [Fact]
    public void Add_infrastructure_rejects_invalid_policy_limits()
    {
        using TemporaryPaths paths =
            new();

        IConfiguration configuration =
            CreateConfiguration(
                paths,
                maxOperations: "2",
                maxCreatedFiles: "3");

        var services =
            new ServiceCollection();

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    services.AddInfrastructure(
                        configuration));

        Assert.Contains(
            "MaxCreatedFiles",
            exception.Message,
            StringComparison.Ordinal);
    }

    private static IConfiguration CreateConfiguration(
        TemporaryPaths paths,
        string maxOperations,
        string maxCreatedFiles)
    {
        var values =
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] =
                    "Host=localhost;Database=kronxy;Username=kronxy",

                ["ExecutionPlane:RepositoryRoot"] =
                    paths.Repository,

                ["ExecutionPlane:WorkspaceRoot"] =
                    paths.Workspace,

                ["ExecutionPlane:DotnetExecutable"] =
                    ResolveExecutable(
                        OperatingSystem.IsWindows()
                            ? "dotnet.exe"
                            : "dotnet"),

                ["ExecutionPlane:GitExecutable"] =
                    ResolveExecutable(
                        OperatingSystem.IsWindows()
                            ? "git.exe"
                            : "git"),

                ["ExecutionPlane:DotnetTarget"] =
                    "Kronxy.sln",

                ["DeveloperPolicy:MaxOperations"] =
                    maxOperations,

                ["DeveloperPolicy:MaxCreatedFiles"] =
                    maxCreatedFiles,

                ["DeveloperPolicy:MaxFileBytes"] =
                    "1024",

                ["DeveloperPolicy:MaxTotalChangeBytes"] =
                    "2048",

                ["DeveloperPolicy:MaxProposalBytes"] =
                    "4096",

                ["DeveloperPolicy:MaxPathDepth"] =
                    "8",

                ["DeveloperPolicy:MaxPathCharacters"] =
                    "120",

                ["ArtifactStore:RootPath"] =
                    paths.Artifacts,

                ["ArtifactStore:MaxArtifactBytes"] =
                    "16777216",

                ["AI:Provider"] =
                    "Ollama",

                ["AI:Endpoint"] =
                    "http://localhost:11434",

                ["AI:Models:CodingFast"] =
                    "test-coding-fast",

                ["AI:Models:CodingQuality"] =
                    "test-coding-quality",

                ["AI:Models:General"] =
                    "test-general",

                ["AI:MaxConcurrentInferences"] =
                    "1",

                ["AI:ConnectionTimeout"] =
                    "00:00:05",

                ["AI:InferenceTimeout"] =
                    "00:01:00",

                ["AI:PlanningInferenceTimeout"] =
                    "00:03:00",

                ["AI:DeveloperInferenceTimeout"] =
                    "00:01:00",

                ["AI:QueueWaitTimeout"] =
                    "00:00:05",

                ["AI:MaxOutputTokens"] =
                    "1024",

                ["AI:MaxInputCharacters"] =
                    "65536",

                ["AI:MaxResponseBytes"] =
                    "1048576"
            };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                values)
            .Build();
    }

    private static string ResolveExecutable(
        string executable)
    {
        string? hostPath =
            executable.StartsWith(
                "dotnet",
                StringComparison.OrdinalIgnoreCase)
                ? Environment.GetEnvironmentVariable(
                    "DOTNET_HOST_PATH")
                : null;

        if (!string.IsNullOrWhiteSpace(hostPath) &&
            Path.IsPathFullyQualified(hostPath) &&
            File.Exists(hostPath))
        {
            return Path.GetFullPath(
                hostPath);
        }

        string? pathValue =
            Environment.GetEnvironmentVariable(
                "PATH");

        if (!string.IsNullOrWhiteSpace(pathValue))
        {
            foreach (
                string directory
                in pathValue.Split(
                    Path.PathSeparator,
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries))
            {
                string candidate =
                    Path.Combine(
                        directory,
                        executable);

                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(
                        candidate);
                }
            }
        }

        throw new InvalidOperationException(
            $"{executable} executable not found.");
    }

    private sealed class TemporaryPaths :
        IDisposable
    {
        public TemporaryPaths()
        {
            Root =
                Path.Combine(
                    Path.GetTempPath(),
                    "kronxy-policy-registration-tests",
                    Guid.NewGuid()
                        .ToString("N"));

            Repository =
                Path.Combine(
                    Root,
                    "repository");

            Workspace =
                Path.Combine(
                    Root,
                    "workspaces");

            Artifacts =
                Path.Combine(
                    Root,
                    "artifacts");

            Directory.CreateDirectory(
                Repository);
        }

        private string Root { get; }

        public string Repository { get; }

        public string Workspace { get; }

        public string Artifacts { get; }

        public void Dispose()
        {
            if (Directory.Exists(
                    Root))
            {
                Directory.Delete(
                    Root,
                    recursive: true);
            }
        }
    }
}

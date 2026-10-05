using System.Security.Cryptography;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;
using Kronxy.Infrastructure.Artifacts;
using Kronxy.Infrastructure.Execution;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class DevelopmentAnalysisServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "kronxy-development-analysis-tests", Guid.NewGuid().ToString("N"));
    private readonly RecordingArtifactStore store = new();

    [Fact]
    public async Task New_component_is_allowed_and_persisted()
    {
        DevelopmentAnalysis result = await Analyze("Domain-only\nsrc/Kronxy.Domain/Widgets/Widget.cs");

        Assert.Equal(DevelopmentChangeClassification.NewComponent, result.PrimaryClassification);
        Assert.True(result.ScopeCompatible);
        Assert.True(result.DeveloperExecutionAllowed);
        Assert.False(result.ArchitectureDecisionRequired);
        Assert.Single(store.Requests);
        Assert.Equal(ArtifactType.DevelopmentAnalysis, store.Requests[0].ArtifactType);
    }

    [Fact]
    public async Task Existing_local_target_distinguishes_extension_refactor_and_ambiguity()
    {
        Write("src/Kronxy.Domain/Widgets/Widget.cs", "public sealed class Widget { }");

        Assert.Equal(DevelopmentChangeClassification.Extension,
            (await Analyze("Domain-only add member\nsrc/Kronxy.Domain/Widgets/Widget.cs")).PrimaryClassification);
        Assert.Equal(DevelopmentChangeClassification.LocalRefactor,
            (await Analyze("Domain-only refactor\nsrc/Kronxy.Domain/Widgets/Widget.cs")).PrimaryClassification);
        DevelopmentAnalysis unknown = await Analyze("Domain-only change\nsrc/Kronxy.Domain/Widgets/Widget.cs");
        Assert.Equal(DevelopmentChangeClassification.Unknown, unknown.PrimaryClassification);
        Assert.False(unknown.DeveloperExecutionAllowed);
        Assert.True(unknown.ArchitectureDecisionRequired);
    }

    [Fact]
    public async Task Explicit_requirements_can_prove_request_already_satisfied()
    {
        Write("src/Kronxy.Domain/Widgets/Widget.cs", "public sealed class Widget { public void Activate() { } }");

        DevelopmentAnalysis result = await Analyze("""
            Domain-only
            src/Kronxy.Domain/Widgets/Widget.cs
            REQUIRED METHODS
            Activate()
            """);

        Assert.Equal(DevelopmentChangeClassification.AlreadySatisfied, result.PrimaryClassification);
        Assert.False(result.DeveloperExecutionAllowed);
        Assert.False(result.ArchitectureDecisionRequired);
    }

    [Fact]
    public async Task Cross_layer_replacement_is_breaking_when_scope_allows_it()
    {
        Write("src/Kronxy.Domain/Widgets/Widget.cs", "public sealed class Widget { }");
        Write("src/Kronxy.Application/Widgets/UseWidget.cs", "public sealed class UseWidget { private Widget? value; }");

        DevelopmentAnalysis result = await Analyze("Cross-layer replace Widget\nsrc/Kronxy.Domain/Widgets/Widget.cs");

        Assert.Equal(DevelopmentChangeClassification.BreakingChange, result.PrimaryClassification);
        Assert.Contains("Domain", result.ImpactedLayers);
        Assert.Contains("Application", result.ImpactedLayers);
        Assert.NotEmpty(result.BreakingContracts);
        Assert.True(result.DeveloperExecutionAllowed);
    }

    [Fact]
    public async Task Cross_layer_addition_is_classified_without_silent_scope_expansion()
    {
        Write("src/Kronxy.Domain/Widgets/Widget.cs", "public sealed class Widget { }");
        Write("src/Kronxy.Application/Widgets/UseWidget.cs", "public sealed class UseWidget { private Widget? value; }");

        DevelopmentAnalysis result = await Analyze("Cross-layer change Widget\nsrc/Kronxy.Domain/Widgets/Widget.cs");

        Assert.Equal(DevelopmentChangeClassification.CrossLayerChange, result.PrimaryClassification);
        Assert.Equal("Cross-layer", result.RequiredScope);
        Assert.True(result.ScopeCompatible);
        Assert.True(result.DeveloperExecutionAllowed);
    }

    [Fact]
    public async Task Governed_artifact_path_is_immutable()
    {
        string artifactRoot = Path.Combine(root, "artifacts");
        Directory.CreateDirectory(artifactRoot);
        var artifactStore = new FileSystemArtifactStore(new ArtifactStoreOptions
        {
            RootPath = artifactRoot,
            MaxArtifactBytes = 1024 * 1024
        });
        var service = new DevelopmentAnalysisService(artifactStore, new DevelopmentAnalysisOptions());
        DevelopmentAnalysisRequest request = Request("Domain-only\nsrc/Kronxy.Domain/Widgets/Widget.cs");

        DevelopmentAnalysisResult first = await service.AnalyzeAsync(request);
        DevelopmentAnalysisResult second = await service.AnalyzeAsync(request);

        Assert.True(first.IsSuccess);
        Assert.Equal($"{request.JobId:N}/{request.RunId:N}/development-analysis/analysis.json", first.Artifact!.RelativePath);
        Assert.False(second.IsSuccess);
        Assert.Equal("DEVELOPMENT_ANALYSIS_ARTIFACT_FAILED", second.ErrorCode);
    }

    [Fact]
    public async Task Domain_only_parent_change_with_cross_layer_consumers_is_architecture_conflict()
    {
        Write("src/Kronxy.Domain/WorkItems/WorkItem.cs", "public sealed class WorkItem { public Guid ExistingParentId { get; private set; } }");
        Write("src/Kronxy.Application/WorkItems/GetWorkItem.cs", "public sealed class GetWorkItem { private WorkItem? item; }");
        Write("src/Kronxy.Infrastructure/WorkItems/WorkItemConfiguration.cs", "public sealed class WorkItemConfiguration { private WorkItem? item; }");
        Write("src/Kronxy.Infrastructure/Migrations/Initial.cs", "public sealed class Initial { private string WorkItem = string.Empty; }");

        DevelopmentAnalysis result = await Analyze("Domain-only parent must be NewParentId\nsrc/Kronxy.Domain/WorkItems/WorkItem.cs");

        Assert.Equal(DevelopmentChangeClassification.ArchitectureConflict, result.PrimaryClassification);
        Assert.Equal("Cross-layer", result.RequiredScope);
        Assert.False(result.ScopeCompatible);
        Assert.True(result.ArchitectureDecisionRequired);
        Assert.False(result.DeveloperExecutionAllowed);
        Assert.Contains("Persistence", result.ImpactedLayers);
    }

    [Fact]
    public async Task Bounded_scan_fails_closed_as_unknown()
    {
        Write("src/Kronxy.Domain/Widgets/Widget.cs", "public sealed class Widget { }");
        Write("src/Kronxy.Application/A.cs", "public sealed class A { private Widget? value; }");
        var service = new DevelopmentAnalysisService(store, new DevelopmentAnalysisOptions { MaxFilesInspected = 1 });

        DevelopmentAnalysisResult result = await service.AnalyzeAsync(Request("Cross-layer replace Widget\nsrc/Kronxy.Domain/Widgets/Widget.cs"));

        Assert.True(result.IsSuccess);
        Assert.Equal(DevelopmentChangeClassification.Unknown, result.Analysis!.PrimaryClassification);
        Assert.False(result.Analysis.DeveloperExecutionAllowed);
    }

    private async Task<DevelopmentAnalysis> Analyze(string jobRequest)
    {
        Directory.CreateDirectory(root);
        var service = new DevelopmentAnalysisService(store, new DevelopmentAnalysisOptions());
        DevelopmentAnalysisResult result = await service.AnalyzeAsync(Request(jobRequest));
        Assert.True(result.IsSuccess, result.ErrorCode);
        return result.Analysis!;
    }

    private DevelopmentAnalysisRequest Request(string jobRequest) => new()
    {
        JobId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        AttemptCount = 1,
        JobRequest = jobRequest,
        Repository = new RepositoryWorktreeHandle(Guid.NewGuid(), "TEST", root, root, "test", "head", RepositoryWorktreeOperationKind.Existing),
        CorrelationId = "development-analysis-test"
    };

    private void Write(string relativePath, string content)
    {
        string path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private sealed class RecordingArtifactStore : IArtifactStore
    {
        public List<ArtifactWriteRequest> Requests { get; } = [];

        public Task<ArtifactWriteResult> WriteAsync(ArtifactWriteRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            byte[] content = request.Content.ToArray();
            return Task.FromResult(ArtifactWriteResult.Success(new ArtifactRecord
            {
                ArtifactId = Guid.NewGuid(), JobId = request.JobId, RunId = request.RunId,
                ArtifactType = request.ArtifactType, RelativePath = "development-analysis/analysis.json",
                Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(), SizeBytes = content.LongLength,
                CreatedAtUtc = DateTimeOffset.UtcNow, CorrelationId = request.CorrelationId
            }));
        }
    }
}

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

    [Theory]
    [InlineData("Domain-only\nsrc/Kronxy.Domain/Widgets/Widget.cs")]
    [InlineData("Create the Domain-only target src/Kronxy.Domain/Widgets/Widget.cs inside prose.")]
    [InlineData("Create the Domain-only target `src/Kronxy.Domain/Widgets/Widget.cs`.")]
    [InlineData("Create Domain-only targets: src/Kronxy.Domain/Widgets/Widget.cs, src/Kronxy.Domain/Widgets/WidgetErrors.cs; src/Kronxy.Domain/Widgets/IWidgetRepository.cs.")]
    public async Task Candidate_paths_are_extracted_from_supported_prose(string request)
    {
        DevelopmentAnalysis result = await Analyze(request);

        Assert.Equal(DevelopmentChangeClassification.NewComponent, result.PrimaryClassification);
        Assert.True(result.ScopeCompatible);
        Assert.True(result.DeveloperExecutionAllowed);
    }

    [Fact]
    public async Task Three_inline_candidate_paths_are_all_extracted()
    {
        DevelopmentAnalysis result = await Analyze(
            "Create Domain-only files src/Kronxy.Domain/Notes/Note.cs, " +
            "src/Kronxy.Domain/Notes/NoteErrors.cs and " +
            "src/Kronxy.Domain/Notes/INoteRepository.cs.");

        Assert.Equal(
            ["INoteRepository", "Note", "NoteErrors"],
            result.TargetSymbols);
        Assert.Equal(3, result.Evidence.Count(item => item.Kind == "TargetAbsent"));
    }

    [Fact]
    public async Task Duplicate_candidate_path_is_deduplicated()
    {
        DevelopmentAnalysis result = await Analyze(
            "Domain-only src/Kronxy.Domain/Widgets/Widget.cs, " +
            "src/Kronxy.Domain/Widgets/Widget.cs");

        Assert.Single(result.TargetSymbols);
        Assert.Single(result.Evidence, item => item.Kind == "TargetAbsent");
    }

    [Theory]
    [InlineData("Domain-only /src/Kronxy.Domain/Widgets/Widget.cs")]
    [InlineData("Domain-only ../src/Kronxy.Domain/Widgets/Widget.cs")]
    [InlineData("Domain-only src/Kronxy.Domain/../Widgets/Widget.cs")]
    [InlineData("Domain-only https://example.test/src/Kronxy.Domain/Widgets/Widget.cs")]
    public async Task Unsafe_or_non_repository_candidate_paths_are_rejected(string request)
    {
        DevelopmentAnalysis result = await Analyze(request);

        Assert.Equal(DevelopmentChangeClassification.Unknown, result.PrimaryClassification);
        Assert.Empty(result.TargetSymbols);
        Assert.True(result.ArchitectureDecisionRequired);
        Assert.False(result.DeveloperExecutionAllowed);
    }

    [Fact]
    public async Task Candidate_limit_exceeded_fails_closed_without_dropping_targets()
    {
        Directory.CreateDirectory(root);
        var service = new DevelopmentAnalysisService(
            store,
            new DevelopmentAnalysisOptions { MaxCandidateTargets = 2 });

        DevelopmentAnalysisResult result = await service.AnalyzeAsync(
            Request(
                "Domain-only src/Kronxy.Domain/Notes/One.cs, " +
                "src/Kronxy.Domain/Notes/Two.cs, " +
                "src/Kronxy.Domain/Notes/Three.cs"));

        Assert.True(result.IsSuccess, result.ErrorCode);
        Assert.Equal(
            DevelopmentChangeClassification.Unknown,
            result.Analysis!.PrimaryClassification);
        Assert.True(result.Analysis.ArchitectureDecisionRequired);
        Assert.False(result.Analysis.DeveloperExecutionAllowed);
        Assert.Contains(
            result.Analysis.Evidence,
            item => item.Kind == "Ambiguity" &&
                    item.Detail.Contains("configured limit", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Greenfield_inline_request_is_allowed_as_domain_only_new_component()
    {
        DevelopmentAnalysis result = await Analyze(
            "Create a greenfield Domain-only component. Create exactly these three files and no others: " +
            "src/Kronxy.Domain/DevelopmentNotes/DevelopmentNote.cs, " +
            "src/Kronxy.Domain/DevelopmentNotes/DevelopmentNoteErrors.cs, " +
            "src/Kronxy.Domain/DevelopmentNotes/IDevelopmentNoteRepository.cs. " +
            "No persistence, API, EF, or migrations.");

        Assert.Equal(3, result.TargetSymbols.Count);
        Assert.Equal(DevelopmentChangeClassification.NewComponent, result.PrimaryClassification);
        Assert.Equal("Domain-only", result.RequestedScope);
        Assert.Equal("Domain-only", result.RequiredScope);
        Assert.True(result.ScopeCompatible);
        Assert.False(result.ArchitectureDecisionRequired);
        Assert.True(result.DeveloperExecutionAllowed);
    }

    [Fact]
    public async Task Domain_only_target_has_domain_only_required_scope()
    {
        DevelopmentAnalysis result = await Analyze(
            "Domain-only src/Kronxy.Domain/Widgets/Widget.cs");

        Assert.Equal(["Domain"], result.ImpactedLayers);
        Assert.Equal("Domain-only", result.RequiredScope);
        Assert.True(result.ScopeCompatible);
    }

    [Fact]
    public async Task Test_impact_remains_visible_without_expanding_domain_scope()
    {
        Write(
            "tests/Kronxy.Domain.Tests/WidgetTests.cs",
            "public sealed class WidgetTests { private Widget? value; }");

        DevelopmentAnalysis result = await Analyze(
            "Domain-only src/Kronxy.Domain/Widgets/Widget.cs");

        Assert.Equal(["Domain", "Tests"], result.ImpactedLayers);
        Assert.Equal("Domain-only", result.RequiredScope);
        Assert.True(result.ScopeCompatible);
        Assert.Contains(
            result.Evidence,
            item => item.Kind == "Reference" &&
                    item.Path == "tests/Kronxy.Domain.Tests/WidgetTests.cs");
    }

    [Theory]
    [InlineData("src/Kronxy.Application/Widgets/UseWidget.cs")]
    [InlineData("src/Kronxy.Infrastructure/Widgets/UseWidget.cs")]
    [InlineData("src/Kronxy.Infrastructure/Migrations/UseWidget.cs")]
    [InlineData("src/Kronxy.Api/Widgets/UseWidget.cs")]
    public async Task Additional_functional_layer_requires_cross_layer_scope(
        string consumerPath)
    {
        Write(
            consumerPath,
            "public sealed class UseWidget { private Widget? value; }");

        DevelopmentAnalysis result = await Analyze(
            "Domain-only src/Kronxy.Domain/Widgets/Widget.cs");

        Assert.Equal("Cross-layer", result.RequiredScope);
        Assert.False(result.ScopeCompatible);
    }

    [Fact]
    public async Task Tests_do_not_change_cross_layer_scope_from_functional_layers()
    {
        Write(
            "src/Kronxy.Application/Widgets/UseWidget.cs",
            "public sealed class UseWidget { private Widget? value; }");
        Write(
            "tests/Kronxy.Application.Tests/WidgetTests.cs",
            "public sealed class WidgetTests { private Widget? value; }");

        DevelopmentAnalysis result = await Analyze(
            "Domain-only src/Kronxy.Domain/Widgets/Widget.cs");

        Assert.Equal(["Application", "Domain", "Tests"], result.ImpactedLayers);
        Assert.Equal("Cross-layer", result.RequiredScope);
        Assert.False(result.ScopeCompatible);
    }

    [Fact]
    public async Task Application_and_tests_remain_application_only_scope()
    {
        Write(
            "tests/Kronxy.Application.Tests/HandlerTests.cs",
            "public sealed class HandlerTests { private NewHandler? value; }");

        DevelopmentAnalysis result = await Analyze(
            "Application-only src/Kronxy.Application/Handlers/NewHandler.cs");

        Assert.Equal(["Application", "Tests"], result.ImpactedLayers);
        Assert.Equal("Application-only", result.RequiredScope);
        Assert.True(result.ScopeCompatible);
    }

    [Fact]
    public async Task Greenfield_inline_request_with_test_references_remains_allowed()
    {
        Write(
            "tests/Kronxy.Domain.Tests/DevelopmentNoteTests.cs",
            "public sealed class DevelopmentNoteTests { " +
            "private DevelopmentNote? note; " +
            "private DevelopmentNoteErrors? errors; " +
            "private IDevelopmentNoteRepository? repository; }");

        DevelopmentAnalysis result = await Analyze(
            "Create a greenfield Domain-only component in " +
            "src/Kronxy.Domain/DevelopmentNotes/DevelopmentNote.cs, " +
            "src/Kronxy.Domain/DevelopmentNotes/DevelopmentNoteErrors.cs, and " +
            "src/Kronxy.Domain/DevelopmentNotes/IDevelopmentNoteRepository.cs.");

        Assert.Equal(DevelopmentChangeClassification.NewComponent, result.PrimaryClassification);
        Assert.Equal(["Domain", "Tests"], result.ImpactedLayers);
        Assert.Equal("Domain-only", result.RequiredScope);
        Assert.True(result.ScopeCompatible);
        Assert.True(result.DeveloperExecutionAllowed);
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

    [Fact]
    public async Task Explicit_razor_path_is_discovered_and_inspected()
    {
        Write("src/Example.Web/Pages/OrderDetail.razor", "<h1>Order detail</h1>");

        DevelopmentAnalysis result = await Analyze(
            "Web-only add usability behavior to src/Example.Web/Pages/OrderDetail.razor");

        Assert.Equal(DevelopmentChangeClassification.Extension, result.PrimaryClassification);
        Assert.Equal("Web-only", result.RequiredScope);
        Assert.Contains("src/Example.Web/Pages/OrderDetail.razor", result.FilesInspected);
    }

    [Fact]
    public async Task Explicit_filename_is_mapped_to_a_unique_inventory_candidate()
    {
        Write("src/Example.Web/Pages/OrderDetail.razor", "<h1>Order detail</h1>");

        DevelopmentAnalysis result = await Analyze("Web-only add behavior to OrderDetail.razor");

        Assert.Contains("src/Example.Web/Pages/OrderDetail.razor", result.FilesInspected);
    }

    [Fact]
    public async Task Duplicate_explicit_filename_matches_fail_closed()
    {
        Write("src/First.Web/Pages/OrderDetail.razor", "<h1>First</h1>");
        Write("src/Second.Web/Pages/OrderDetail.razor", "<h1>Second</h1>");

        DevelopmentAnalysis result = await Analyze("Add behavior to OrderDetail.razor");

        Assert.Equal(DevelopmentChangeClassification.Unknown, result.PrimaryClassification);
        Assert.Empty(result.FilesInspected);
    }

    [Theory]
    [InlineData("Add a usability improvement to the existing Example.Web Job Detail page.")]
    [InlineData("Add a usability improvement to the existing job-detail page.")]
    [InlineData("Add a usability improvement to the existing job_detail page.")]
    public async Task Compound_component_name_is_discovered_from_natural_language(string request)
    {
        Write("src/Example.Web/Components/Pages/JobDetail.razor", "<h1>Job detail</h1>");
        Write("src/Example.Web/Components/Pages/Jobs.razor", "<h1>Jobs</h1>");
        Write("src/Example.Web/Components/Pages/CreateJob.razor", "<h1>Create job</h1>");

        DevelopmentAnalysis result = await Analyze(request);

        Assert.Equal(DevelopmentChangeClassification.Extension, result.PrimaryClassification);
        Assert.Equal("Web-only", result.RequiredScope);
        Assert.Contains("src/Example.Web/Components/Pages/JobDetail.razor", result.FilesInspected);
    }

    [Fact]
    public async Task Web_target_and_tests_keep_web_only_functional_scope()
    {
        Write("src/Example.Web/Pages/JobDetail.razor", "<h1>Job detail</h1>");
        Write("tests/Example.Web.Tests/JobDetailTests.cs", "public sealed class JobDetailTests { }");

        DevelopmentAnalysis result = await Analyze(
            "Web-only add behavior in src/Example.Web/Pages/JobDetail.razor and tests/Example.Web.Tests/JobDetailTests.cs");

        Assert.Equal(["Tests", "Web"], result.ImpactedLayers);
        Assert.Equal("Web-only", result.RequiredScope);
        Assert.True(result.ScopeCompatible);
    }

    [Theory]
    [InlineData("Web-only add styling in src/Example.Web/Pages/JobDetail.css", "src/Example.Web/Pages/JobDetail.css")]
    [InlineData("Web-only add script behavior in src/Example.Web/Pages/JobDetail.js", "src/Example.Web/Pages/JobDetail.js")]
    public async Task Explicit_style_and_script_targets_are_supported(string request, string path)
    {
        Write(path, "/* safe text fixture */");

        DevelopmentAnalysis result = await Analyze(request);

        Assert.Contains(path, result.FilesInspected);
        Assert.Equal("Web-only", result.RequiredScope);
    }

    [Theory]
    [InlineData("src/Example.Web/bin/Debug/Generated.razor")]
    [InlineData("src/Example.Web/obj/Debug/Generated.razor")]
    [InlineData("src/Example.Web/Generated.g.cs")]
    public async Task Generated_output_candidates_are_excluded(string path)
    {
        Write(path, "generated");

        DevelopmentAnalysis result = await Analyze($"Add behavior to {path}");

        Assert.Equal(DevelopmentChangeClassification.Unknown, result.PrimaryClassification);
        Assert.Empty(result.FilesInspected);
    }

    [Fact]
    public async Task Unsupported_binary_candidate_is_excluded()
    {
        Write("src/Example.Web/Pages/JobDetail.png", "not an image");

        DevelopmentAnalysis result = await Analyze("Add behavior to src/Example.Web/Pages/JobDetail.png");

        Assert.Equal(DevelopmentChangeClassification.Unknown, result.PrimaryClassification);
        Assert.Empty(result.FilesInspected);
    }

    [Fact]
    public async Task Equally_relevant_unrelated_components_fail_closed()
    {
        Write("src/Example.Web/Pages/OrderItem.razor", "<p>One</p>");
        Write("src/Example.Web/Pages/ItemOrder.razor", "<p>Two</p>");

        DevelopmentAnalysis result = await Analyze("Add behavior to the order item page");

        Assert.Equal(DevelopmentChangeClassification.Unknown, result.PrimaryClassification);
        Assert.Empty(result.FilesInspected);
        Assert.Contains(result.Evidence, item => item.Detail.Contains("equal relevance", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ambiguous_request_without_component_hints_remains_unknown()
    {
        Write("src/Example.Web/Pages/JobDetail.razor", "<h1>Job detail</h1>");

        DevelopmentAnalysis result = await Analyze("Improve the operator experience");

        Assert.Equal(DevelopmentChangeClassification.Unknown, result.PrimaryClassification);
        Assert.Empty(result.FilesInspected);
    }

    [Fact]
    public async Task Representative_frontend_inventory_selects_job_detail_without_exact_path()
    {
        Write("src/Example.Web/Components/Pages/JobDetail.razor", "<h1>Job detail</h1>");
        Write("src/Example.Web/Components/Pages/Jobs.razor", "<h1>Jobs</h1>");
        Write("src/Example.Web/Components/Pages/CreateJob.razor", "<h1>Create job</h1>");
        Write("src/Example.Web/Clients/ExampleApiClient.cs", "public sealed class ExampleApiClient { }");
        Write("src/Example.Web/Models/ApiContracts.cs", "public sealed class ApiContracts { }");
        Write("tests/Example.Web.Tests/ExampleApiClientTests.cs", "public sealed class ExampleApiClientTests { }");

        DevelopmentAnalysis result = await Analyze(
            "Add a usability improvement to the existing Example.Web Job Detail page.");

        Assert.Equal(DevelopmentChangeClassification.Extension, result.PrimaryClassification);
        Assert.Contains("src/Example.Web/Components/Pages/JobDetail.razor", result.FilesInspected);
        Assert.Equal(["JobDetail"], result.TargetSymbols);
        Assert.NotEqual("Unknown", result.RequiredScope);
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

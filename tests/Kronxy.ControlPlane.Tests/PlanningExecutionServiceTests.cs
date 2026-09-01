using System.Text;
using Kronxy.Application.AI;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Context;
using Kronxy.Application.Execution;
using Kronxy.Infrastructure.AI;
using Kronxy.Infrastructure.Artifacts;
using Kronxy.Infrastructure.Execution;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class PlanningExecutionServiceTests
{
    [Fact]
    public async Task Success_persists_ai_response()
    {
        Fixture fixture = CreateFixture();

        PlanningExecutionResult result =
            await fixture.Service.ExecuteAsync(
                Request());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Report);
        Assert.NotNull(result.AiResponseArtifact);
        Assert.Equal(
            ArtifactType.AiResponse,
            fixture.Store.LastRequest!.ArtifactType);
        Assert.Equal(1, fixture.Gateway.CallCount);
    }

    [Fact]
    public async Task Artifact_read_failure_stops_before_ai()
    {
        Fixture fixture = CreateFixture();

        fixture.Reader.Result =
            ArtifactReadResult.Failure(
                ArtifactReadFailureKind.NotFound,
                "NOT_FOUND");

        PlanningExecutionResult result =
            await fixture.Service.ExecuteAsync(
                Request());

        Assert.False(result.IsSuccess);
        Assert.Equal(
            PlanningExecutionFailureKind
                .ContextArtifactReadFailure,
            result.FailureKind);
        Assert.Equal(0, fixture.Gateway.CallCount);
    }

    [Fact]
    public async Task Invalid_context_stops_before_ai()
    {
        Fixture fixture = CreateFixture();

        fixture.Context.Result =
            ContextAiInputResult.Failure(
                ContextAiInputFailureKind.InvalidPackage,
                "BAD_PACKAGE");

        PlanningExecutionResult result =
            await fixture.Service.ExecuteAsync(
                Request());

        Assert.False(result.IsSuccess);
        Assert.Equal(
            PlanningExecutionFailureKind
                .ContextPackageInvalid,
            result.FailureKind);
        Assert.Equal(0, fixture.Gateway.CallCount);
    }

    [Fact]
    public async Task Ai_timeout_is_mapped()
    {
        Fixture fixture = CreateFixture();

        fixture.Gateway.Response =
            new AiResponse
            {
                Status = AiOperationStatus.TimedOut,
                ErrorCode = "AI_TIMEOUT"
            };

        PlanningExecutionResult result =
            await fixture.Service.ExecuteAsync(
                Request());

        Assert.False(result.IsSuccess);
        Assert.Equal(
            PlanningExecutionFailureKind.AiTimedOut,
            result.FailureKind);
        Assert.Null(fixture.Store.LastRequest);
    }

    [Fact]
    public async Task Ai_provider_failure_is_mapped()
    {
        Fixture fixture = CreateFixture();

        fixture.Gateway.Response =
            new AiResponse
            {
                Status =
                    AiOperationStatus.ProviderUnavailable,
                ErrorCode = "AI_UNAVAILABLE"
            };

        PlanningExecutionResult result =
            await fixture.Service.ExecuteAsync(
                Request());

        Assert.False(result.IsSuccess);
        Assert.Equal(
            PlanningExecutionFailureKind.AiUnavailable,
            result.FailureKind);
    }

    [Fact]
    public async Task Artifact_write_failure_fails_closed()
    {
        Fixture fixture = CreateFixture();

        fixture.Store.Result =
            ArtifactWriteResult.Failure(
                ArtifactStoreFailureKind.IoFailure,
                "WRITE_FAILED");

        PlanningExecutionResult result =
            await fixture.Service.ExecuteAsync(
                Request());

        Assert.False(result.IsSuccess);
        Assert.Equal(
            PlanningExecutionFailureKind
                .ArtifactWriteFailure,
            result.FailureKind);
    }

    [Fact]
    public async Task Invalid_request_never_calls_dependencies()
    {
        Fixture fixture = CreateFixture();

        PlanningExecutionResult result =
            await fixture.Service.ExecuteAsync(
                new PlanningExecutionRequest
                {
                    JobId = Guid.Empty,
                    RunId = Guid.NewGuid(),
                    JobRequest = "x",
                    CorrelationId = "corr"
                });

        Assert.False(result.IsSuccess);
        Assert.Equal(
            PlanningExecutionFailureKind.InvalidRequest,
            result.FailureKind);
        Assert.Equal(0, fixture.Reader.CallCount);
        Assert.Equal(0, fixture.Gateway.CallCount);
    }

    private static PlanningExecutionRequest Request() =>
        new()
        {
            JobId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            JobRequest = "Implement feature safely.",
            CorrelationId = "corr-1"
        };

    private static Fixture CreateFixture()
    {
        var reader = new FakeReader();
        var context = new FakeContextBuilder();
        var gateway = new FakeGateway();
        var store = new FakeStore();

        var artifactOptions =
            new ArtifactStoreOptions
            {
                RootPath =
                    Path.GetFullPath(
                        Path.Combine(
                            Path.GetTempPath(),
                            "kronxy-planning-tests")),
                MaxArtifactBytes =
                    16_777_216
            };

        var aiOptions =
            new AiGatewayOptions
            {
                Provider = "Ollama",

                Endpoint =
                    "http://localhost:11434",

                Models =
                    new Dictionary<string, string>
                    {
                        ["CodingFast"] =
                            "model-fast",

                        ["CodingQuality"] =
                            "model-quality",

                        ["General"] =
                            "model-general"
                    },

                MaxConcurrentInferences = 2,

                ConnectionTimeout =
                    TimeSpan.FromSeconds(1),

                InferenceTimeout =
                    TimeSpan.FromSeconds(60),

                QueueWaitTimeout =
                    TimeSpan.FromSeconds(1),

                MaxOutputTokens = 4_096,

                MaxInputCharacters = 65_536,

                MaxResponseBytes = 1_048_576
            };

        var service =
            new PlanningExecutionService(
                reader,
                context,
                gateway,
                store,
                artifactOptions,
                aiOptions);

        return new Fixture(
            service,
            reader,
            context,
            gateway,
            store);
    }

    private sealed record Fixture(
        PlanningExecutionService Service,
        FakeReader Reader,
        FakeContextBuilder Context,
        FakeGateway Gateway,
        FakeStore Store);

    private sealed class FakeReader :
        IArtifactReader
    {
        public int CallCount { get; private set; }

        public ArtifactReadResult Result { get; set; } =
            ArtifactReadResult.Success(
                new ArtifactRecord
                {
                    ArtifactId = Guid.NewGuid(),
                    JobId = Guid.NewGuid(),
                    RunId = Guid.NewGuid(),
                    ArtifactType =
                        ArtifactType.ContextPackage,
                    RelativePath = "context/context.zip",
                    Sha256 = new string('a', 64),
                    SizeBytes = 3,
                    CreatedAtUtc =
                        DateTimeOffset.UtcNow,
                    CorrelationId = "corr"
                },
                new byte[] { 1, 2, 3 });

        public Task<ArtifactReadResult> ReadAsync(
            ArtifactReadRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeContextBuilder :
        IContextAiInputBuilder
    {
        public ContextAiInputResult Result { get; set; } =
            ContextAiInputResult.Success(
                "===== FILE: src/a.cs =====\nclass A {}\n");

        public Task<ContextAiInputResult> BuildAsync(
            ContextAiInputRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result);
    }

    private sealed class FakeGateway :
        IAiGateway
    {
        public int CallCount { get; private set; }

        public AiResponse Response { get; set; } =
            new()
            {
                Status = AiOperationStatus.Success,
                Content = "Plan",
                Provider = "Ollama",
                LogicalModel = "CodingQuality",
                PhysicalModel = "quality",
                Duration =
                    TimeSpan.FromMilliseconds(10),
                TerminationReason =
                    AiTerminationReason.Stop,
                Usage = new AiUsage(10, 5)
            };

        public Task<AiResponse> GenerateAsync(
            AiRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(Response);
        }

        public Task<AiProviderHealthResult> CheckHealthAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                new AiProviderHealthResult(
                    AiProviderHealthStatus.Available,
                    "Ollama",
                    Array.Empty<string>(),
                    string.Empty));
    }

    private sealed class FakeStore :
        IArtifactStore
    {
        public ArtifactWriteRequest? LastRequest { get; private set; }

        public ArtifactWriteResult Result { get; set; } =
            ArtifactWriteResult.Success(
                new ArtifactRecord
                {
                    ArtifactId = Guid.NewGuid(),
                    JobId = Guid.NewGuid(),
                    RunId = Guid.NewGuid(),
                    ArtifactType =
                        ArtifactType.AiResponse,
                    RelativePath = "ai/response.json",
                    Sha256 = new string('b', 64),
                    SizeBytes = 1,
                    CreatedAtUtc =
                        DateTimeOffset.UtcNow,
                    CorrelationId = "corr"
                });

        public Task<ArtifactWriteResult> WriteAsync(
            ArtifactWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }
}

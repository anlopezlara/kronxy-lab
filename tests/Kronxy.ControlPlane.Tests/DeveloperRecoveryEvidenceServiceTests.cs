using System.Text.Json;
using Kronxy.Application.AI;
using Kronxy.Application.Artifacts;
using Kronxy.Application.Execution;
using Kronxy.Infrastructure.Artifacts;
using Kronxy.Infrastructure.Execution;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class DeveloperRecoveryEvidenceServiceTests
{
    [Fact]
    public async Task No_artifacts_is_not_completed() =>
        Assert.Equal(StageRecoveryStatus.NotCompleted,
            (await Service(new Reader()).CheckAsync(Request())).Status);

    [Theory]
    [InlineData(ArtifactType.DeveloperProposal)]
    [InlineData(ArtifactType.DeveloperResponse)]
    public async Task Partial_evidence_is_invalid(ArtifactType present)
    {
        var reader = new Reader
        {
            Handler = request => request.ArtifactType == present
                ? Success(request, Content(request.ArtifactType))
                : ArtifactReadResult.Failure(
                    ArtifactReadFailureKind.NotFound, "NOT_FOUND")
        };
        StageRecoveryResult result =
            await Service(reader).CheckAsync(Request());
        Assert.Equal(StageRecoveryStatus.InvalidEvidence, result.Status);
    }

    [Fact]
    public async Task Both_valid_artifacts_recover_validated_proposal()
    {
        var reader = BothValid();
        StageRecoveryResult result =
            await Service(reader).CheckAsync(Request());
        Assert.True(result.IsCompleted);
        Assert.NotNull(result.DeveloperProposal);
        Assert.Equal("src/New.cs",
            Assert.Single(result.DeveloperProposal.Changes).RelativePath);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"summary\":\"incomplete\"}")]
    public async Task Invalid_json_or_schema_is_invalid(string proposal)
    {
        var reader = BothValid();
        reader.Proposal = System.Text.Encoding.UTF8.GetBytes(proposal);
        StageRecoveryResult result =
            await Service(reader).CheckAsync(Request());
        Assert.Equal(StageRecoveryStatus.InvalidEvidence, result.Status);
    }

    [Fact]
    public async Task Policy_invalid_proposal_is_invalid()
    {
        var reader = BothValid();
        reader.Proposal = JsonSerializer.SerializeToUtf8Bytes(
            Proposal("../escape.cs"));
        StageRecoveryResult result =
            await Service(reader).CheckAsync(Request());
        Assert.Equal(StageRecoveryStatus.InvalidEvidence, result.Status);
    }

    [Fact]
    public async Task Integrity_failure_is_invalid()
    {
        var reader = BothValid();
        reader.Handler = request =>
            request.ArtifactType == ArtifactType.DeveloperProposal
                ? ArtifactReadResult.Failure(
                    ArtifactReadFailureKind.IntegrityFailure,
                    "HASH_MISMATCH")
                : request.ArtifactType == ArtifactType.DeveloperResponse
                    ? Success(request, Content(request.ArtifactType))
                    : ArtifactReadResult.Failure(
                        ArtifactReadFailureKind.NotFound,
                        "NOT_FOUND");
        StageRecoveryResult result =
            await Service(reader).CheckAsync(Request());
        Assert.Equal(StageRecoveryStatus.InvalidEvidence, result.Status);
        Assert.Equal("HASH_MISMATCH", result.ErrorCode);
    }

    private static StageRecoveryEvidenceService Service(Reader reader) =>
        new(reader,
            new ArtifactStoreOptions
            {
                RootPath = "/tmp/kronxy-developer-recovery-tests",
                MaxArtifactBytes = 16_777_216
            },
            new DeveloperProposalPolicy(
                new DeveloperChangePolicyOptions()),
            new Kronxy.Infrastructure.AI.AiStructuredOutputValidator());

    private static Reader BothValid() => new()
    {
        Handler = request =>
            request.ArtifactType is ArtifactType.DeveloperProposal or
                ArtifactType.DeveloperResponse
                ? Success(request, Content(request.ArtifactType))
                : ArtifactReadResult.Failure(
                    ArtifactReadFailureKind.NotFound,
                    "NOT_FOUND")
    };

    private static StageRecoveryRequest Request() => new()
    {
        JobId = Guid.NewGuid(), RunId = Guid.NewGuid(),
        Stage = RecoveryStage.Developer, CorrelationId = "recovery"
    };

    private static byte[] Content(ArtifactType type) =>
        type == ArtifactType.DeveloperProposal
            ? JsonSerializer.SerializeToUtf8Bytes(Proposal("src/New.cs"))
            : JsonSerializer.SerializeToUtf8Bytes(new AiResponse
            {
                Status = AiOperationStatus.Success,
                Content = "{}", Provider = "Fake",
                LogicalModel = "CodingQuality", PhysicalModel = "fake"
            });

    private static DeveloperProposal Proposal(string path) => new()
    {
        Summary = "Recovered proposal",
        Changes = new[]
        {
            new DeveloperChangeOperation
            {
                Operation = DeveloperChangeOperationType.CreateFile,
                RelativePath = path,
                Intent = "Create deterministic file.",
                Content = "class NewType {}",
                ExpectedContentSha256 = string.Empty
            }
        },
        Assumptions = Array.Empty<string>(),
        Risks = Array.Empty<string>()
    };

    private static ArtifactReadResult Success(
        ArtifactReadRequest request, byte[] content) =>
        ArtifactReadResult.Success(new ArtifactRecord
        {
            ArtifactId = Guid.NewGuid(), JobId = request.JobId,
            RunId = request.RunId, ArtifactType = request.ArtifactType,
            RelativePath = "developer/evidence.json",
            Sha256 = new string('a', 64), SizeBytes = content.Length,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = request.CorrelationId
        }, content);

    private sealed class Reader : IArtifactReader
    {
        public Func<ArtifactReadRequest, ArtifactReadResult>? Handler { get; set; }
        public byte[]? Proposal { get; set; }
        public Task<ArtifactReadResult> ReadAsync(
            ArtifactReadRequest request,
            CancellationToken cancellationToken = default)
        {
            if (Proposal is not null &&
                request.ArtifactType == ArtifactType.DeveloperProposal)
                return Task.FromResult(Success(request, Proposal));
            return Task.FromResult(Handler?.Invoke(request) ??
                ArtifactReadResult.Failure(
                    ArtifactReadFailureKind.NotFound, "NOT_FOUND"));
        }
    }
}

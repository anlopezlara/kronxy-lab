using Kronxy.Application.Execution;
using Kronxy.Infrastructure.Execution;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class DeveloperProposalPolicyTests
{
    [Fact]
    public void Valid_create_and_replace_are_accepted()
    {
        var policy =
            CreatePolicy();

        DeveloperProposal proposal =
            Proposal(
                Change(
                    DeveloperChangeOperationType.CreateFile,
                    "src/NewFile.cs",
                    string.Empty),
                Change(
                    DeveloperChangeOperationType.ReplaceFile,
                    "src/Existing.cs",
                    new string('A', 64)));

        DeveloperProposalPolicyResult result =
            policy.Validate(
                proposal);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Proposal);
        Assert.Equal(
            2,
            result.Proposal.Changes.Count);
        Assert.Equal(
            new string('a', 64),
            result.Proposal
                .Changes[1]
                .ExpectedContentSha256);
        Assert.True(
            result.Proposal.TotalChangeBytes > 0);
    }

    [Fact]
    public void Verbose_metadata_is_rejected_by_policy()
    {
        var policy = CreatePolicy();
        DeveloperProposal proposal = Proposal(
            Change(DeveloperChangeOperationType.CreateFile, "src/A.cs", string.Empty))
            with { Summary = new string((char)120, 257) };

        AssertFailure(
            policy.Validate(proposal),
            DeveloperProposalFailureKind.InvalidProposal,
            "DEVELOPER_PROPOSAL_INVALID");
    }

    [Fact]
    public void Unknown_operation_is_rejected()
    {
        var policy =
            CreatePolicy();

        DeveloperProposalPolicyResult result =
            policy.Validate(
                Proposal(
                    Change(
                        (DeveloperChangeOperationType)999,
                        "src/File.cs",
                        string.Empty)));

        AssertFailure(
            result,
            DeveloperProposalFailureKind.UnsupportedOperation,
            "DEVELOPER_OPERATION_UNSUPPORTED");
    }

    [Fact]
    public void Unsafe_paths_are_rejected()
    {
        string[] paths =
        [
            "../outside.cs",
            "src/../outside.cs",
            "/tmp/outside.cs",
            "C:\\outside.cs",
            "\\\\server\\share.cs",
            "src//File.cs",
            "src/./File.cs"
        ];

        var policy =
            CreatePolicy();

        foreach (string path in paths)
        {
            DeveloperProposalPolicyResult result =
                policy.Validate(
                    Proposal(
                        Change(
                            DeveloperChangeOperationType.CreateFile,
                            path,
                            string.Empty)));

            AssertFailure(
                result,
                DeveloperProposalFailureKind.InvalidPath,
                "DEVELOPER_PATH_INVALID");
        }
    }

    [Fact]
    public void Protected_paths_are_rejected()
    {
        string[] paths =
        [
            ".git/config",
            ".github/workflows/build.yml",
            "AGENTS.md",
            "docs/AGENTS.md",
            ".env",
            "src/Kronxy.Api/appsettings.json"
        ];

        var policy =
            CreatePolicy();

        foreach (string path in paths)
        {
            DeveloperProposalPolicyResult result =
                policy.Validate(
                    Proposal(
                        Change(
                            DeveloperChangeOperationType.CreateFile,
                            path,
                            string.Empty)));

            AssertFailure(
                result,
                DeveloperProposalFailureKind.ProtectedPath,
                "DEVELOPER_PATH_PROTECTED");
        }
    }

    [Fact]
    public void Duplicate_paths_are_rejected()
    {
        var policy =
            CreatePolicy();

        DeveloperProposalPolicyResult result =
            policy.Validate(
                Proposal(
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "src/File.cs",
                        string.Empty),
                    Change(
                        DeveloperChangeOperationType.ReplaceFile,
                        "SRC/file.cs",
                        new string('a', 64))));

        AssertFailure(
            result,
            DeveloperProposalFailureKind.DuplicatePath,
            "DEVELOPER_PATH_DUPLICATE");
    }

    [Fact]
    public void Operation_and_create_limits_are_enforced()
    {
        var operationPolicy =
            CreatePolicy(
                new DeveloperChangePolicyOptions
                {
                    MaxOperations = 1,
                    MaxCreatedFiles = 1
                });

        DeveloperProposalPolicyResult operations =
            operationPolicy.Validate(
                Proposal(
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "src/A.cs",
                        string.Empty),
                    Change(
                        DeveloperChangeOperationType.ReplaceFile,
                        "src/B.cs",
                        new string('a', 64))));

        AssertFailure(
            operations,
            DeveloperProposalFailureKind.TooManyOperations,
            "DEVELOPER_TOO_MANY_OPERATIONS");

        var createPolicy =
            CreatePolicy(
                new DeveloperChangePolicyOptions
                {
                    MaxOperations = 3,
                    MaxCreatedFiles = 1
                });

        DeveloperProposalPolicyResult creates =
            createPolicy.Validate(
                Proposal(
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "src/A.cs",
                        string.Empty),
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "src/B.cs",
                        string.Empty)));

        AssertFailure(
            creates,
            DeveloperProposalFailureKind.TooManyCreatedFiles,
            "DEVELOPER_TOO_MANY_CREATED_FILES");
    }

    [Fact]
    public void Three_creates_are_accepted_with_job01_limits()
    {
        var policy = CreatePolicy(
            new DeveloperChangePolicyOptions
            {
                MaxOperations = 4,
                MaxCreatedFiles = 3
            });

        DeveloperProposalPolicyResult result = policy.Validate(
            Proposal(
                Change(DeveloperChangeOperationType.CreateFile, "src/A.cs", string.Empty),
                Change(DeveloperChangeOperationType.CreateFile, "src/B.cs", string.Empty),
                Change(DeveloperChangeOperationType.CreateFile, "src/C.cs", string.Empty)));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Proposal!.Changes.Count);
    }

    [Fact]
    public void Four_creates_are_rejected_with_job01_limits()
    {
        var policy = CreatePolicy(
            new DeveloperChangePolicyOptions
            {
                MaxOperations = 4,
                MaxCreatedFiles = 3
            });

        DeveloperProposalPolicyResult result = policy.Validate(
            Proposal(
                Change(DeveloperChangeOperationType.CreateFile, "src/A.cs", string.Empty),
                Change(DeveloperChangeOperationType.CreateFile, "src/B.cs", string.Empty),
                Change(DeveloperChangeOperationType.CreateFile, "src/C.cs", string.Empty),
                Change(DeveloperChangeOperationType.CreateFile, "src/D.cs", string.Empty)));

        AssertFailure(
            result,
            DeveloperProposalFailureKind.TooManyCreatedFiles,
            "DEVELOPER_TOO_MANY_CREATED_FILES");
    }

    [Fact]
    public void More_than_four_operations_remain_rejected()
    {
        var policy = CreatePolicy(
            new DeveloperChangePolicyOptions
            {
                MaxOperations = 4,
                MaxCreatedFiles = 3
            });

        DeveloperProposalPolicyResult result = policy.Validate(
            Proposal(
                Change(DeveloperChangeOperationType.CreateFile, "src/A.cs", string.Empty),
                Change(DeveloperChangeOperationType.CreateFile, "src/B.cs", string.Empty),
                Change(DeveloperChangeOperationType.CreateFile, "src/C.cs", string.Empty),
                Change(DeveloperChangeOperationType.ReplaceFile, "src/D.cs", new string('a', 64)),
                Change(DeveloperChangeOperationType.ReplaceFile, "src/E.cs", new string('b', 64))));

        AssertFailure(
            result,
            DeveloperProposalFailureKind.TooManyOperations,
            "DEVELOPER_TOO_MANY_OPERATIONS");
    }

    [Fact]
    public void File_and_total_byte_limits_are_enforced()
    {
        var filePolicy =
            CreatePolicy(
                new DeveloperChangePolicyOptions
                {
                    MaxFileBytes = 3,
                    MaxTotalChangeBytes = 6
                });

        DeveloperProposalPolicyResult file =
            filePolicy.Validate(
                Proposal(
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "src/A.cs",
                        string.Empty,
                        "1234")));

        AssertFailure(
            file,
            DeveloperProposalFailureKind.FileTooLarge,
            "DEVELOPER_FILE_TOO_LARGE");

        var totalPolicy =
            CreatePolicy(
                new DeveloperChangePolicyOptions
                {
                    MaxFileBytes = 4,
                    MaxTotalChangeBytes = 7
                });

        DeveloperProposalPolicyResult total =
            totalPolicy.Validate(
                Proposal(
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "src/A.cs",
                        string.Empty,
                        "1234"),
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "src/B.cs",
                        string.Empty,
                        "5678")));

        AssertFailure(
            total,
            DeveloperProposalFailureKind
                .TotalChangeBytesExceeded,
            "DEVELOPER_TOTAL_BYTES_EXCEEDED");
    }

    [Fact]
    public void Proposal_byte_limit_is_enforced()
    {
        var policy =
            CreatePolicy(
                new DeveloperChangePolicyOptions
                {
                    MaxFileBytes = 8,
                    MaxTotalChangeBytes = 8,
                    MaxProposalBytes = 16
                });

        DeveloperProposalPolicyResult result =
            policy.Validate(
                Proposal(
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "src/A.cs",
                        string.Empty)));

        AssertFailure(
            result,
            DeveloperProposalFailureKind.ProposalTooLarge,
            "DEVELOPER_PROPOSAL_TOO_LARGE");
    }

    [Fact]
    public void Expected_hash_semantics_are_enforced()
    {
        var policy =
            CreatePolicy();

        DeveloperProposalPolicyResult createHash =
            policy.Validate(
                Proposal(
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "src/A.cs",
                        new string('a', 64))));

        AssertFailure(
            createHash,
            DeveloperProposalFailureKind.InvalidExpectedHash,
            "DEVELOPER_EXPECTED_HASH_INVALID");

        DeveloperProposalPolicyResult emptyReplaceHash =
            policy.Validate(
                Proposal(
                    Change(
                        DeveloperChangeOperationType.ReplaceFile,
                        "src/A.cs",
                        string.Empty)));

        AssertFailure(
            emptyReplaceHash,
            DeveloperProposalFailureKind.InvalidExpectedHash,
            "DEVELOPER_EXPECTED_HASH_INVALID");

        DeveloperProposalPolicyResult malformedReplaceHash =
            policy.Validate(
                Proposal(
                    Change(
                        DeveloperChangeOperationType.ReplaceFile,
                        "src/A.cs",
                        new string('z', 64))));

        AssertFailure(
            malformedReplaceHash,
            DeveloperProposalFailureKind.InvalidExpectedHash,
            "DEVELOPER_EXPECTED_HASH_INVALID");
    }

    [Fact]
    public void Partially_invalid_proposal_returns_no_output()
    {
        var policy =
            CreatePolicy();

        DeveloperProposalPolicyResult result =
            policy.Validate(
                Proposal(
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "src/Valid.cs",
                        string.Empty),
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "../Invalid.cs",
                        string.Empty)));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Proposal);
        Assert.Equal(
            DeveloperProposalFailureKind.InvalidPath,
            result.FailureKind);
    }

    private static DeveloperProposalPolicy CreatePolicy(
        DeveloperChangePolicyOptions? options = null)
    {
        return new DeveloperProposalPolicy(
            options ??
            new DeveloperChangePolicyOptions());
    }

    private static DeveloperProposal Proposal(
        params DeveloperChangeOperation[] changes)
    {
        return new DeveloperProposal
        {
            Summary = "Apply deterministic source changes.",
            Changes = changes,
            Assumptions = [],
            Risks = []
        };
    }

    private static DeveloperChangeOperation Change(
        DeveloperChangeOperationType operation,
        string path,
        string expectedHash,
        string content = "namespace Example;")
    {
        return new DeveloperChangeOperation
        {
            Operation = operation,
            RelativePath = path,
            Intent = "Apply the requested source change.",
            Content = content,
            ExpectedContentSha256 = expectedHash
        };
    }

    private static void AssertFailure(
        DeveloperProposalPolicyResult result,
        DeveloperProposalFailureKind expectedKind,
        string expectedCode)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Proposal);
        Assert.Equal(
            expectedKind,
            result.FailureKind);
        Assert.Equal(
            expectedCode,
            result.ErrorCode);
    }
}

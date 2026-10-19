using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;
using Kronxy.Infrastructure.Execution;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class SafeChangeWorkspaceValidatorTests
{
    [Fact]
    public async Task Valid_create_and_replace_are_prepared()
    {
        using var fixture =
            new Fixture();

        string existingPath =
            Path.Combine(
                fixture.RepositoryPath,
                "src",
                "Existing.cs");

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                existingPath)!);

        await File.WriteAllTextAsync(
            existingPath,
            "before");

        string beforeHash =
            Hash(
                "before");

        SafeChangeWorkspaceValidationResult result =
            await fixture.Validator.ValidateAsync(
                fixture.Request(
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "src/New.cs",
                        string.Empty,
                        "new"),
                    Change(
                        DeveloperChangeOperationType.ReplaceFile,
                        "src/Existing.cs",
                        beforeHash,
                        "after")));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Targets);
        Assert.Equal(
            2,
            result.Targets.Count);
        Assert.Null(
            result.Targets[0].ExistingSha256);
        Assert.Equal(
            beforeHash,
            result.Targets[1].ExistingSha256);
    }

    [Fact]
    public async Task Replace_binding_uses_real_workspace_hash_and_ignores_ai_hash()
    {
        using var fixture = new Fixture();
        string path = Path.Combine(fixture.RepositoryPath, "src", "Existing.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "governed content");

        DeveloperProposalMetadataBindingResult result =
            await fixture.Validator.BindAsync(fixture.BindingRequest(
                RawChange(
                    DeveloperChangeOperationType.ReplaceFile,
                    "src/Existing.cs",
                    "invalid-ai-hash",
                    "replacement"),
                ["src/Existing.cs"]));

        Assert.True(result.IsSuccess);
        Assert.Equal(
            Hash("governed content"),
            Assert.Single(result.Proposal!.Changes).ExpectedContentSha256);
    }

    [Fact]
    public async Task Localized_patch_applies_exact_edits_and_computes_result_hash()
    {
        using var fixture = new Fixture();
        const string before = "alpha\nbeta\ngamma\n";
        const string after = "one\nbeta\nthree\n";
        string path = Path.Combine(fixture.RepositoryPath, "src", "Large.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, before);

        DeveloperProposalMetadataBindingResult result =
            await fixture.Validator.BindAsync(fixture.BindingRequest(
                RawPatch("src/Large.cs", Hash(before),
                    [("alpha", "one"), ("gamma", "three")]),
                ["src/Large.cs"]));

        Assert.True(result.IsSuccess, result.ErrorCode);
        DeveloperChangeOperation change = Assert.Single(result.Proposal!.Changes);
        Assert.Equal(DeveloperChangeOperationType.LocalizedPatch, change.Operation);
        Assert.Equal(before, await File.ReadAllTextAsync(path));
        Assert.Equal(after, change.Content);
        Assert.Equal(Hash(before), change.ExpectedContentSha256);
        Assert.Equal(Hash(after), change.ResultContentSha256);
    }

    [Theory]
    [InlineData("missing", "DEVELOPER_LOCALIZED_PATCH_NO_MATCH")]
    [InlineData("repeat", "DEVELOPER_LOCALIZED_PATCH_AMBIGUOUS")]
    public async Task Localized_patch_requires_one_exact_match(
        string oldText,
        string expectedError)
    {
        using var fixture = new Fixture();
        const string before = "repeat middle repeat";
        string path = Path.Combine(fixture.RepositoryPath, "src", "Large.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, before);

        DeveloperProposalMetadataBindingResult result =
            await fixture.Validator.BindAsync(fixture.BindingRequest(
                RawPatch("src/Large.cs", Hash(before), [(oldText, "new")]),
                ["src/Large.cs"]));

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.ErrorCode);
    }

    [Fact]
    public async Task Localized_patch_rejects_base_drift_and_overlap()
    {
        using var fixture = new Fixture();
        const string before = "abcdef";
        string path = Path.Combine(fixture.RepositoryPath, "src", "Large.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, before);

        DeveloperProposalMetadataBindingResult drift =
            await fixture.Validator.BindAsync(fixture.BindingRequest(
                RawPatch("src/Large.cs", new string('a', 64), [("abc", "x")]),
                ["src/Large.cs"]));
        Assert.Equal("DEVELOPER_LOCALIZED_PATCH_BASE_SHA_MISMATCH", drift.ErrorCode);

        DeveloperProposalMetadataBindingResult overlap =
            await fixture.Validator.BindAsync(fixture.BindingRequest(
                RawPatch("src/Large.cs", Hash(before),
                    [("abcd", "x"), ("cdef", "y")]),
                ["src/Large.cs"]));
        Assert.Equal("DEVELOPER_LOCALIZED_PATCH_OVERLAP", overlap.ErrorCode);
    }

    [Fact]
    public async Task Binding_rejects_path_outside_allowlist_before_hashing()
    {
        using var fixture = new Fixture();
        string path = Path.Combine(fixture.RepositoryPath, "src", "Outside.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "outside");

        DeveloperProposalMetadataBindingResult result =
            await fixture.Validator.BindAsync(fixture.BindingRequest(
                RawChange(
                    DeveloperChangeOperationType.ReplaceFile,
                    "src/Outside.cs",
                    string.Empty,
                    "replacement"),
                ["src/Allowed.cs"]));

        Assert.False(result.IsSuccess);
        Assert.Equal("DEVELOPER_PATH_NOT_IN_PLAN", result.ErrorCode);
    }

    [Fact]
    public async Task Binding_rejects_missing_replace_source()
    {
        using var fixture = new Fixture();

        DeveloperProposalMetadataBindingResult result =
            await fixture.Validator.BindAsync(fixture.BindingRequest(
                RawChange(
                    DeveloperChangeOperationType.ReplaceFile,
                    "src/Missing.cs",
                    string.Empty,
                    "replacement"),
                ["src/Missing.cs"]));

        Assert.False(result.IsSuccess);
        Assert.Equal("DEVELOPER_REPLACE_SOURCE_INVALID", result.ErrorCode);
    }

    [Fact]
    public async Task Binding_is_deterministic_for_unchanged_content()
    {
        using var fixture = new Fixture();
        string path = Path.Combine(fixture.RepositoryPath, "src", "Stable.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "stable");
        DeveloperProposalMetadataBindingRequest request = fixture.BindingRequest(
            RawChange(
                DeveloperChangeOperationType.ReplaceFile,
                "src/Stable.cs",
                "first-ai-value",
                "replacement"),
            ["src/Stable.cs"]);

        DeveloperProposalMetadataBindingResult first =
            await fixture.Validator.BindAsync(request);
        DeveloperProposalMetadataBindingResult second =
            await fixture.Validator.BindAsync(request);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(
            Assert.Single(first.Proposal!.Changes).ExpectedContentSha256,
            Assert.Single(second.Proposal!.Changes).ExpectedContentSha256);
    }

    [Fact]
    public async Task SafeChange_rejects_file_changed_after_binding()
    {
        using var fixture = new Fixture();
        string path = Path.Combine(fixture.RepositoryPath, "src", "Changed.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "before");
        DeveloperProposalMetadataBindingResult binding =
            await fixture.Validator.BindAsync(fixture.BindingRequest(
                RawChange(
                    DeveloperChangeOperationType.ReplaceFile,
                    "src/Changed.cs",
                    string.Empty,
                    "replacement"),
                ["src/Changed.cs"]));
        Assert.True(binding.IsSuccess);
        var policy = new DeveloperProposalPolicy(
            new DeveloperChangePolicyOptions());
        DeveloperProposalPolicyResult validated =
            policy.Validate(binding.Proposal);
        Assert.True(validated.IsSuccess);

        await File.WriteAllTextAsync(path, "changed after binding");
        SafeChangeWorkspaceValidationResult result =
            await fixture.Validator.ValidateAsync(
                fixture.Request(validated.Proposal!.Changes.ToArray()));

        AssertFailure(
            result,
            SafeChangeApplicationFailureKind.PreconditionFailed,
            "SAFE_CHANGE_REPLACE_HASH_MISMATCH");
    }

    [Fact]
    public async Task Repository_outside_workspace_is_rejected()
    {
        using var fixture =
            new Fixture();

        RepositoryWorktreeHandle forged =
            fixture.Repository with
            {
                RepositoryPath =
                    fixture.AuthoritativeRepository
            };

        SafeChangeWorkspaceValidationResult result =
            await fixture.Validator.ValidateAsync(
                fixture.Request(
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "src/New.cs",
                        string.Empty,
                        "new")) with
                {
                    Repository = forged
                });

        AssertFailure(
            result,
            SafeChangeApplicationFailureKind.UnsafeWorkspace,
            "SAFE_CHANGE_WORKSPACE_UNSAFE");
    }

    [Fact]
    public async Task Forged_and_protected_paths_are_rejected()
    {
        using var fixture =
            new Fixture();

        SafeChangeWorkspaceValidationResult traversal =
            await fixture.Validator.ValidateAsync(
                fixture.Request(
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "../outside.cs",
                        string.Empty,
                        "unsafe")));

        AssertFailure(
            traversal,
            SafeChangeApplicationFailureKind.InvalidPath,
            "SAFE_CHANGE_PATH_INVALID");

        SafeChangeWorkspaceValidationResult protectedPath =
            await fixture.Validator.ValidateAsync(
                fixture.Request(
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "AGENTS.md",
                        string.Empty,
                        "unsafe")));

        AssertFailure(
            protectedPath,
            SafeChangeApplicationFailureKind.ProtectedPath,
            "SAFE_CHANGE_PATH_PROTECTED");
    }

    [Fact]
    public async Task Existing_create_and_hash_mismatch_are_rejected()
    {
        using var fixture =
            new Fixture();

        string existing =
            Path.Combine(
                fixture.RepositoryPath,
                "Existing.cs");

        await File.WriteAllTextAsync(
            existing,
            "before");

        SafeChangeWorkspaceValidationResult create =
            await fixture.Validator.ValidateAsync(
                fixture.Request(
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "Existing.cs",
                        string.Empty,
                        "new")));

        AssertFailure(
            create,
            SafeChangeApplicationFailureKind
                .DestinationConflict,
            "SAFE_CHANGE_CREATE_DESTINATION_EXISTS");

        SafeChangeWorkspaceValidationResult replace =
            await fixture.Validator.ValidateAsync(
                fixture.Request(
                    Change(
                        DeveloperChangeOperationType.ReplaceFile,
                        "Existing.cs",
                        new string('a', 64),
                        "after")));

        AssertFailure(
            replace,
            SafeChangeApplicationFailureKind
                .PreconditionFailed,
            "SAFE_CHANGE_REPLACE_HASH_MISMATCH");
    }

    [Fact]
    public async Task Workspace_ownership_mismatch_is_rejected()
    {
        using var fixture =
            new Fixture();

        File.WriteAllText(
            Path.Combine(
                fixture.WorkspacePath,
                ".kronxy-workspace.json"),
            JsonSerializer.Serialize(
                new
                {
                    Version = 1,
                    JobId = Guid.NewGuid(),
                    JobExternalId =
                        fixture.ExternalId
                }));

        SafeChangeWorkspaceValidationResult result =
            await fixture.Validator.ValidateAsync(
                fixture.Request(
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "src/New.cs",
                        string.Empty,
                        "new")));

        AssertFailure(
            result,
            SafeChangeApplicationFailureKind.UnsafeWorkspace,
            "SAFE_CHANGE_WORKSPACE_OWNERSHIP_INVALID");
    }

    [Fact]
    public async Task Intermediate_symlink_is_rejected()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture =
            new Fixture();

        string outside =
            Path.Combine(
                fixture.Root,
                "outside");

        Directory.CreateDirectory(
            outside);

        string linkedDirectory =
            Path.Combine(
                fixture.RepositoryPath,
                "linked");

        Directory.CreateSymbolicLink(
            linkedDirectory,
            outside);

        SafeChangeWorkspaceValidationResult result =
            await fixture.Validator.ValidateAsync(
                fixture.Request(
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "linked/New.cs",
                        string.Empty,
                        "new")));

        AssertFailure(
            result,
            SafeChangeApplicationFailureKind.SymlinkDetected,
            "SAFE_CHANGE_TARGET_SYMLINK_DETECTED");

        Assert.Empty(
            Directory.EnumerateFileSystemEntries(
                outside));
    }

    private static ValidatedDeveloperChange Change(
        DeveloperChangeOperationType operation,
        string path,
        string expectedHash,
        string content)
    {
        return new ValidatedDeveloperChange(
            operation,
            path,
            "Apply a deterministic change.",
            content,
            expectedHash,
            Encoding.UTF8.GetByteCount(
                content));
    }

    private static DeveloperChangeOperation RawChange(
        DeveloperChangeOperationType operation,
        string path,
        string expectedHash,
        string content) =>
        new()
        {
            Operation = operation,
            RelativePath = path,
            Intent = "Apply a deterministic change.",
            Content = content,
            ExpectedContentSha256 = expectedHash
        };

    private static DeveloperChangeOperation RawPatch(
        string path,
        string expectedHash,
        IReadOnlyList<(string OldText, string NewText)> edits) =>
        new()
        {
            Operation = DeveloperChangeOperationType.LocalizedPatch,
            RelativePath = path,
            Intent = "Apply deterministic localized edits.",
            Content = string.Empty,
            ExpectedContentSha256 = expectedHash,
            Edits = edits.Select(edit => new DeveloperLocalizedEdit
            {
                OldText = edit.OldText,
                NewText = edit.NewText
            }).ToArray(),
            ResultContentSha256 = new string('f', 64)
        };

    private static string Hash(
        string content)
    {
        return Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        content)))
            .ToLowerInvariant();
    }

    private static void AssertFailure(
        SafeChangeWorkspaceValidationResult result,
        SafeChangeApplicationFailureKind kind,
        string errorCode)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Targets);
        Assert.Equal(
            kind,
            result.FailureKind);
        Assert.Equal(
            errorCode,
            result.ErrorCode);
    }

    private sealed class Fixture :
        IDisposable
    {
        public Fixture()
        {
            Root =
                Path.Combine(
                    Path.GetTempPath(),
                    "kronxy-safe-change-validator-tests",
                    Guid.NewGuid()
                        .ToString("N"));

            AuthoritativeRepository =
                Path.Combine(
                    Root,
                    "authoritative");

            WorkspaceRoot =
                Path.Combine(
                    Root,
                    "workspaces");

            JobId =
                Guid.NewGuid();

            RunId =
                Guid.NewGuid();

            ExternalId =
                "KRX-123456";

            WorkspacePath =
                Path.Combine(
                    WorkspaceRoot,
                    "job-workspace");

            RepositoryPath =
                Path.Combine(
                    WorkspacePath,
                    "repository");

            Directory.CreateDirectory(
                AuthoritativeRepository);

            Directory.CreateDirectory(
                RepositoryPath);

            File.WriteAllText(
                Path.Combine(
                    WorkspacePath,
                    ".kronxy-workspace.json"),
                JsonSerializer.Serialize(
                    new
                    {
                        Version = 1,
                        JobId,
                        JobExternalId =
                            ExternalId
                    }));

            Repository =
                new RepositoryWorktreeHandle(
                    JobId,
                    ExternalId,
                    WorkspacePath,
                    RepositoryPath,
                    "kronxy/jobs/test",
                    new string('a', 40),
                    RepositoryWorktreeOperationKind.Existing);

            var executionOptions =
                new ExecutionPlaneOptions
                {
                    RepositoryRoot =
                        AuthoritativeRepository,

                    WorkspaceRoot =
                        WorkspaceRoot,

                    DotnetExecutable =
                        ResolveExecutable(
                            OperatingSystem.IsWindows()
                                ? "dotnet.exe"
                                : "dotnet"),

                    GitExecutable =
                        ResolveExecutable(
                            OperatingSystem.IsWindows()
                                ? "git.exe"
                                : "git"),

                    DotnetTarget =
                        "Kronxy.sln"
                };

            var policy =
                new DeveloperProposalPolicy(
                    new DeveloperChangePolicyOptions());

            Validator =
                new SafeChangeWorkspaceValidator(
                    executionOptions,
                    policy);
        }

        public string Root { get; }

        public string AuthoritativeRepository { get; }

        public string WorkspaceRoot { get; }

        public string WorkspacePath { get; }

        public string RepositoryPath { get; }

        public Guid JobId { get; }

        public Guid RunId { get; }

        public string ExternalId { get; }

        public RepositoryWorktreeHandle Repository
        {
            get;
        }

        public SafeChangeWorkspaceValidator Validator
        {
            get;
        }

        public SafeChangeApplicationRequest Request(
            params ValidatedDeveloperChange[] changes)
        {
            long total =
                changes.Sum(
                    change =>
                        (long)change.ContentBytes);

            return new SafeChangeApplicationRequest
            {
                JobId =
                    JobId,

                RunId =
                    RunId,

                Repository =
                    Repository,

                Proposal =
                    new ValidatedDeveloperProposal(
                        "Apply deterministic changes.",
                        changes,
                        [],
                        [],
                        total,
                        total),

                CorrelationId =
                    "test-correlation"
            };
        }

        public DeveloperProposalMetadataBindingRequest BindingRequest(
            DeveloperChangeOperation change,
            IReadOnlyList<string> allowedPaths) =>
            new()
            {
                JobId = JobId,
                Repository = Repository,
                Proposal = new DeveloperProposal
                {
                    Summary = "Apply deterministic change.",
                    Changes = [change],
                    Assumptions = [],
                    Risks = []
                },
                AllowedPaths = allowedPaths
            };

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
    }
}

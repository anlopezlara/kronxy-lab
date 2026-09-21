using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;
using Kronxy.Infrastructure.Execution;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class SafeChangeApplierTests
{
    [Fact]
    public async Task Create_and_replace_are_applied()
    {
        using var fixture =
            new Fixture();

        fixture.Write(
            "src/Existing.cs",
            "before");

        SafeChangeApplicationResult result =
            await fixture.Applier.ApplyAsync(
                fixture.Request(
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "src/New.cs",
                        string.Empty,
                        "new"),
                    Change(
                        DeveloperChangeOperationType.ReplaceFile,
                        "src/Existing.cs",
                        Hash("before"),
                        "after")));

        Assert.True(result.IsSuccess, result.ErrorCode);
        Assert.NotNull(result.Report);
        Assert.Equal(
            2,
            result.Report.Changes.Count);
        Assert.Equal(
            "new",
            fixture.Read(
                "src/New.cs"));
        Assert.Equal(
            "after",
            fixture.Read(
                "src/Existing.cs"));
        Assert.False(
            Directory.Exists(
                fixture.TransactionPath));
    }

    [Fact]
    public async Task Successful_apply_creates_durable_completion_receipt()
    {
        using var fixture = new Fixture();
        SafeChangeApplicationResult result = await fixture.Applier.ApplyAsync(
            fixture.Request(Change(DeveloperChangeOperationType.CreateFile, "src/New.cs", string.Empty, "new")));

        Assert.True(result.IsSuccess);
        Assert.True(File.Exists(fixture.ReceiptPath));
        Assert.False(Directory.Exists(fixture.TransactionPath));
    }

    [Fact]
    public async Task Build_correction_uses_separate_receipt_with_same_job_and_run()
    {
        using var fixture = new Fixture();
        SafeChangeApplicationRequest original = fixture.Request(
            Change(
                DeveloperChangeOperationType.CreateFile,
                "src/New.cs",
                string.Empty,
                "new"));

        Assert.True((await fixture.Applier.ApplyAsync(original)).IsSuccess);
        string originalReceipt =
            SafeChangeCompletionReceipt.GetPath(original);
        SafeChangeApplicationRequest correction = fixture.Request(
            Change(
                DeveloperChangeOperationType.ReplaceFile,
                "src/New.cs",
                Hash("new"),
                "corrected")) with
            {
                IsBuildCorrection = true
            };

        SafeChangeApplicationResult result =
            await fixture.Applier.ApplyAsync(correction);

        Assert.True(result.IsSuccess, result.ErrorCode);
        string correctionReceipt =
            SafeChangeCompletionReceipt.GetPath(correction);
        Assert.NotEqual(originalReceipt, correctionReceipt);
        Assert.True(File.Exists(originalReceipt));
        Assert.True(File.Exists(correctionReceipt));
        Assert.Equal("corrected", fixture.Read("src/New.cs"));
    }

    [Fact]
    public async Task Create_second_apply_is_idempotent()
    {
        using var fixture = new Fixture();
        SafeChangeApplicationRequest request = fixture.Request(
            Change(DeveloperChangeOperationType.CreateFile, "src/New.cs", string.Empty, "new"));

        Assert.True((await fixture.Applier.ApplyAsync(request)).IsSuccess);
        Assert.True((await fixture.Applier.ApplyAsync(request)).IsSuccess);
        Assert.Equal("new", fixture.Read("src/New.cs"));
    }

    [Fact]
    public async Task Replace_second_apply_is_idempotent()
    {
        using var fixture = new Fixture();
        fixture.Write("src/Existing.cs", "before");
        SafeChangeApplicationRequest request = fixture.Request(
            Change(DeveloperChangeOperationType.ReplaceFile, "src/Existing.cs", Hash("before"), "after"));

        Assert.True((await fixture.Applier.ApplyAsync(request)).IsSuccess);
        string firstHash = Hash(fixture.Read("src/Existing.cs"));
        Assert.True((await fixture.Applier.ApplyAsync(request)).IsSuccess);
        Assert.Equal(firstHash, Hash(fixture.Read("src/Existing.cs")));
    }

    [Fact]
    public async Task Different_proposal_with_same_ids_fails_closed()
    {
        using var fixture = new Fixture();
        SafeChangeApplicationRequest original = fixture.Request(
            Change(DeveloperChangeOperationType.CreateFile, "src/New.cs", string.Empty, "new"));
        Assert.True((await fixture.Applier.ApplyAsync(original)).IsSuccess);

        SafeChangeApplicationResult result = await fixture.Applier.ApplyAsync(
            fixture.Request(Change(DeveloperChangeOperationType.CreateFile, "src/New.cs", string.Empty, "different")));

        Assert.False(result.IsSuccess);
        Assert.Equal("SAFE_CHANGE_RECEIPT_PROPOSAL_MISMATCH", result.ErrorCode);
        Assert.Equal("new", fixture.Read("src/New.cs"));
    }

    [Fact]
    public async Task Invalid_receipt_json_fails_closed()
    {
        using var fixture = new Fixture();
        SafeChangeApplicationRequest request = fixture.Request(
            Change(DeveloperChangeOperationType.CreateFile, "src/New.cs", string.Empty, "new"));
        Assert.True((await fixture.Applier.ApplyAsync(request)).IsSuccess);
        File.WriteAllText(fixture.ReceiptPath, "not-json");

        SafeChangeApplicationResult result = await fixture.Applier.ApplyAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("SAFE_CHANGE_RECEIPT_INVALID", result.ErrorCode);
        Assert.True(File.Exists(fixture.ReceiptPath));
    }

    [Fact]
    public async Task Structurally_tampered_receipt_fails_closed()
    {
        using var fixture = new Fixture();
        SafeChangeApplicationRequest request = fixture.Request(
            Change(DeveloperChangeOperationType.CreateFile, "src/New.cs", string.Empty, "new"));
        Assert.True((await fixture.Applier.ApplyAsync(request)).IsSuccess);
        File.WriteAllText(fixture.ReceiptPath, "{}");

        SafeChangeApplicationResult result = await fixture.Applier.ApplyAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("SAFE_CHANGE_RECEIPT_IDENTITY_INVALID", result.ErrorCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Missing_or_changed_completed_target_fails_closed(bool delete)
    {
        using var fixture = new Fixture();
        SafeChangeApplicationRequest request = fixture.Request(
            Change(DeveloperChangeOperationType.CreateFile, "src/New.cs", string.Empty, "new"));
        Assert.True((await fixture.Applier.ApplyAsync(request)).IsSuccess);
        if (delete) File.Delete(fixture.PathOf("src/New.cs"));
        else fixture.Write("src/New.cs", "externally changed");

        SafeChangeApplicationResult result = await fixture.Applier.ApplyAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("SAFE_CHANGE_RECEIPT_TARGET", result.ErrorCode);
    }

    [Fact]
    public async Task Symlinked_completed_target_fails_closed()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = new Fixture();
        SafeChangeApplicationRequest request = fixture.Request(
            Change(DeveloperChangeOperationType.CreateFile, "src/New.cs", string.Empty, "new"));
        Assert.True((await fixture.Applier.ApplyAsync(request)).IsSuccess);
        fixture.Write("src/Other.cs", "new");
        File.Delete(fixture.PathOf("src/New.cs"));
        File.CreateSymbolicLink(fixture.PathOf("src/New.cs"), fixture.PathOf("src/Other.cs"));

        SafeChangeApplicationResult result = await fixture.Applier.ApplyAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("SAFE_CHANGE_RECEIPT_TARGET_UNSAFE", result.ErrorCode);
    }

    [Fact]
    public async Task Receipt_before_cleanup_is_recognized_and_transaction_is_cleaned()
    {
        using var fixture = new Fixture();
        SafeChangeApplicationRequest request = fixture.Request(
            Change(DeveloperChangeOperationType.CreateFile, "src/New.cs", string.Empty, "new"));
        Assert.True((await fixture.Applier.ApplyAsync(request)).IsSuccess);
        Directory.CreateDirectory(fixture.TransactionPath);
        File.WriteAllText(Path.Combine(fixture.TransactionPath, "stale.tmp"), "stale");

        SafeChangeApplicationResult result = await fixture.CreateApplier().ApplyAsync(request);

        Assert.True(result.IsSuccess);
        Assert.False(Directory.Exists(fixture.TransactionPath));
    }

    [Fact]
    public async Task Recreated_applier_recognizes_completion_without_process_memory()
    {
        using var fixture = new Fixture();
        SafeChangeApplicationRequest request = fixture.Request(
            Change(DeveloperChangeOperationType.CreateFile, "src/New.cs", string.Empty, "new"));
        Assert.True((await fixture.Applier.ApplyAsync(request)).IsSuccess);

        SafeChangeApplicationResult result = await fixture.CreateApplier().ApplyAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("new", fixture.Read("src/New.cs"));
    }

    [Fact]
    public async Task Invalid_batch_leaves_workspace_unchanged()
    {
        using var fixture =
            new Fixture();

        fixture.Write(
            "src/Existing.cs",
            "before");

        SafeChangeApplicationResult result =
            await fixture.Applier.ApplyAsync(
                fixture.Request(
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "src/New.cs",
                        string.Empty,
                        "new"),
                    Change(
                        DeveloperChangeOperationType.ReplaceFile,
                        "src/Existing.cs",
                        new string('a', 64),
                        "after")));

        Assert.False(result.IsSuccess);
        Assert.False(
            File.Exists(
                fixture.PathOf(
                    "src/New.cs")));
        Assert.Equal(
            "before",
            fixture.Read(
                "src/Existing.cs"));
    }

    [Fact]
    public async Task Failure_after_create_rolls_back_file_and_directories()
    {
        using var fixture =
            new Fixture(
                new ThrowAfterCommitObserver(
                    operationIndex: 0));

        SafeChangeApplicationResult result =
            await fixture.Applier.ApplyAsync(
                fixture.Request(
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "new/deep/File.cs",
                        string.Empty,
                        "created"),
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "src/Second.cs",
                        string.Empty,
                        "second")));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            SafeChangeApplicationFailureKind.IoFailure,
            result.FailureKind);
        Assert.False(
            File.Exists(
                fixture.PathOf(
                    "new/deep/File.cs")));
        Assert.False(
            Directory.Exists(
                fixture.PathOf(
                    "new")));
    }

    [Fact]
    public async Task Failure_after_replace_restores_original()
    {
        using var fixture =
            new Fixture(
                new ThrowAfterCommitObserver(
                    operationIndex: 0));

        fixture.Write(
            "src/Existing.cs",
            "before");

        SafeChangeApplicationResult result =
            await fixture.Applier.ApplyAsync(
                fixture.Request(
                    Change(
                        DeveloperChangeOperationType.ReplaceFile,
                        "src/Existing.cs",
                        Hash("before"),
                        "after"),
                    Change(
                        DeveloperChangeOperationType.CreateFile,
                        "src/New.cs",
                        string.Empty,
                        "new")));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            SafeChangeApplicationFailureKind.IoFailure,
            result.FailureKind);
        Assert.Equal(
            "before",
            fixture.Read(
                "src/Existing.cs"));
        Assert.False(
            File.Exists(
                fixture.PathOf(
                    "src/New.cs")));
    }

    [Fact]
    public async Task Interrupted_create_is_recovered_before_retry()
    {
        using var fixture =
            new Fixture();

        SafeChangeApplicationRequest request =
            fixture.Request(
                Change(
                    DeveloperChangeOperationType.CreateFile,
                    "new/deep/File.cs",
                    string.Empty,
                    "created"));

        Directory.CreateDirectory(
            fixture.TransactionPath);

        string stagePath =
            Path.Combine(
                fixture.TransactionPath,
                "stage-0000.tmp");

        File.WriteAllText(
            stagePath,
            "created",
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false));

        SafeChangeJournalResult journal =
            await SafeChangeTransactionJournal.WriteAsync(
                request,
                fixture.TransactionPath);

        Assert.True(journal.IsSuccess);

        string destination =
            fixture.PathOf(
                "new/deep/File.cs");

        Directory.CreateDirectory(
            Path.GetDirectoryName(destination)!);

        File.Move(
            stagePath,
            destination);

        SafeChangeApplicationResult result =
            await fixture.Applier.ApplyAsync(
                request);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "created",
            fixture.Read(
                "new/deep/File.cs"));
        Assert.False(
            Directory.Exists(
                fixture.TransactionPath));
    }

    [Fact]
    public async Task Interrupted_replace_is_recovered_before_retry()
    {
        using var fixture =
            new Fixture();

        fixture.Write(
            "src/Existing.cs",
            "before");

        SafeChangeApplicationRequest request =
            fixture.Request(
                Change(
                    DeveloperChangeOperationType.ReplaceFile,
                    "src/Existing.cs",
                    Hash("before"),
                    "after"));

        Directory.CreateDirectory(
            fixture.TransactionPath);

        string stagePath =
            Path.Combine(
                fixture.TransactionPath,
                "stage-0000.tmp");

        string backupPath =
            Path.Combine(
                fixture.TransactionPath,
                "backup-0000.bin");

        File.WriteAllText(
            stagePath,
            "after",
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false));

        SafeChangeJournalResult journal =
            await SafeChangeTransactionJournal.WriteAsync(
                request,
                fixture.TransactionPath);

        Assert.True(journal.IsSuccess);

        File.Replace(
            stagePath,
            fixture.PathOf("src/Existing.cs"),
            backupPath,
            ignoreMetadataErrors: true);

        SafeChangeApplicationResult result =
            await fixture.Applier.ApplyAsync(
                request);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "after",
            fixture.Read(
                "src/Existing.cs"));
        Assert.False(
            Directory.Exists(
                fixture.TransactionPath));
    }

    [Fact]
    public async Task Tampered_recovery_manifest_blocks_retry()
    {
        using var fixture =
            new Fixture();

        SafeChangeApplicationRequest request =
            fixture.Request(
                Change(
                    DeveloperChangeOperationType.CreateFile,
                    "src/New.cs",
                    string.Empty,
                    "new"));

        Directory.CreateDirectory(
            fixture.TransactionPath);

        string stagePath =
            Path.Combine(
                fixture.TransactionPath,
                "stage-0000.tmp");

        File.WriteAllText(
            stagePath,
            "new",
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false));

        SafeChangeJournalResult journal =
            await SafeChangeTransactionJournal.WriteAsync(
                request,
                fixture.TransactionPath);

        Assert.True(journal.IsSuccess);

        File.WriteAllText(
            Path.Combine(
                fixture.TransactionPath,
                "manifest.json"),
            "{}");

        SafeChangeApplicationResult result =
            await fixture.Applier.ApplyAsync(
                request);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            SafeChangeApplicationFailureKind.AtomicityFailure,
            result.FailureKind);
        Assert.Equal(
            "SAFE_CHANGE_RECOVERY_MANIFEST_INVALID",
            result.ErrorCode);
        Assert.False(
            File.Exists(
                fixture.PathOf("src/New.cs")));
        Assert.True(
            Directory.Exists(
                fixture.TransactionPath));
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

    private static string Hash(
        string content)
    {
        return Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        content)))
            .ToLowerInvariant();
    }

    private sealed class ThrowAfterCommitObserver :
        ISafeChangeCommitObserver
    {
        private readonly int operationIndex;

        public ThrowAfterCommitObserver(
            int operationIndex)
        {
            this.operationIndex =
                operationIndex;
        }

        public void AfterCommit(
            int operationIndex)
        {
            if (operationIndex ==
                this.operationIndex)
            {
                throw new IOException(
                    "Injected commit failure.");
            }
        }
    }

    private sealed class Fixture :
        IDisposable
    {
        public Fixture(
            ISafeChangeCommitObserver? observer = null)
        {
            Root =
                Path.Combine(
                    Path.GetTempPath(),
                    "kronxy-safe-change-applier-tests",
                    Guid.NewGuid()
                        .ToString("N"));

            string authoritative =
                Path.Combine(
                    Root,
                    "authoritative");

            string workspaceRoot =
                Path.Combine(
                    Root,
                    "workspaces");

            JobId =
                Guid.NewGuid();

            RunId =
                Guid.NewGuid();

            string externalId =
                "KRX-123456";

            WorkspacePath =
                Path.Combine(
                    workspaceRoot,
                    "job-workspace");

            RepositoryPath =
                Path.Combine(
                    WorkspacePath,
                    "repository");

            Directory.CreateDirectory(
                authoritative);

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
                            externalId
                    }));

            Repository =
                new RepositoryWorktreeHandle(
                    JobId,
                    externalId,
                    WorkspacePath,
                    RepositoryPath,
                    "kronxy/jobs/test",
                    new string('a', 40),
                    RepositoryWorktreeOperationKind.Existing);

            var executionOptions =
                new ExecutionPlaneOptions
                {
                    RepositoryRoot =
                        authoritative,

                    WorkspaceRoot =
                        workspaceRoot,

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

            Applier =
                new SafeChangeApplier(
                    Validator,
                    observer ??
                    new NoOpSafeChangeCommitObserver());
        }

        public string Root { get; }

        public string WorkspacePath { get; }

        public string RepositoryPath { get; }

        public Guid JobId { get; }

        public Guid RunId { get; }

        public RepositoryWorktreeHandle Repository
        {
            get;
        }

        public SafeChangeApplier Applier { get; }

        public SafeChangeWorkspaceValidator Validator { get; }

        public SafeChangeApplier CreateApplier() =>
            new(Validator, new NoOpSafeChangeCommitObserver());

        public string ReceiptPath =>
            SafeChangeCompletionReceipt.GetPath(
                Request(Change(
                    DeveloperChangeOperationType.CreateFile,
                    "placeholder.cs",
                    string.Empty,
                    "placeholder")));

        public string TransactionPath =>
            Path.Combine(
                WorkspacePath,
                ".kronxy",
                "change-transactions",
                $"{JobId:N}-{RunId:N}");

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

        public string PathOf(
            string relativePath)
        {
            return Path.Combine(
                RepositoryPath,
                relativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
        }

        public void Write(
            string relativePath,
            string content)
        {
            string path =
                PathOf(
                    relativePath);

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    path)!);

            File.WriteAllText(
                path,
                content);
        }

        public string Read(
            string relativePath)
        {
            return File.ReadAllText(
                PathOf(
                    relativePath));
        }

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

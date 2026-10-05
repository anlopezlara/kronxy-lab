using System.Security.Cryptography;
using System.Text;
using Kronxy.Application.Execution;
using Kronxy.Application.Repositories;
using Kronxy.Infrastructure.Execution;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class SafeChangeTransactionJournalTests
{
    [Fact]
    public async Task Missing_transaction_requires_no_recovery()
    {
        using var fixture =
            new Fixture();

        SafeChangeJournalResult result =
            await SafeChangeTransactionJournal.RecoverAsync(
                fixture.Request(
                    fixture.Create(
                        "src/New.cs",
                        "new")),
                fixture.TransactionPath);

        Assert.True(result.IsSuccess);
        Assert.False(result.WasRecovered);
    }

    [Fact]
    public async Task Interrupted_create_is_rolled_back()
    {
        using var fixture =
            new Fixture();

        SafeChangeApplicationRequest request =
            fixture.Request(
                fixture.Create(
                    "new/deep/File.cs",
                    "created"));

        fixture.PrepareJournalStages(
            request);

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
            fixture.StagePath(0),
            destination);

        SafeChangeJournalResult recovery =
            await SafeChangeTransactionJournal.RecoverAsync(
                request,
                fixture.TransactionPath);

        Assert.True(recovery.IsSuccess);
        Assert.True(recovery.WasRecovered);
        Assert.False(File.Exists(destination));
        Assert.False(
            Directory.Exists(
                fixture.PathOf("new")));
        Assert.False(
            Directory.Exists(
                fixture.TransactionPath));
    }

    [Fact]
    public async Task Interrupted_replace_is_restored()
    {
        using var fixture =
            new Fixture();

        fixture.Write(
            "src/Existing.cs",
            "before");

        SafeChangeApplicationRequest request =
            fixture.Request(
                fixture.Replace(
                    "src/Existing.cs",
                    "before",
                    "after"));

        fixture.PrepareJournalStages(
            request);

        SafeChangeJournalResult journal =
            await SafeChangeTransactionJournal.WriteAsync(
                request,
                fixture.TransactionPath);

        Assert.True(journal.IsSuccess);

        File.Replace(
            fixture.StagePath(0),
            fixture.PathOf("src/Existing.cs"),
            fixture.BackupPath(0),
            ignoreMetadataErrors: true);

        SafeChangeJournalResult recovery =
            await SafeChangeTransactionJournal.RecoverAsync(
                request,
                fixture.TransactionPath);

        Assert.True(recovery.IsSuccess);
        Assert.True(recovery.WasRecovered);
        Assert.Equal(
            "before",
            fixture.Read("src/Existing.cs"));
        Assert.False(
            Directory.Exists(
                fixture.TransactionPath));
    }

    [Fact]
    public async Task Staged_replace_recovery_is_idempotent()
    {
        using var fixture =
            new Fixture();

        fixture.Write(
            "src/Existing.cs",
            "before");

        SafeChangeApplicationRequest request =
            fixture.Request(
                fixture.Replace(
                    "src/Existing.cs",
                    "before",
                    "after"));

        fixture.PrepareJournalStages(
            request);

        SafeChangeJournalResult journal =
            await SafeChangeTransactionJournal.WriteAsync(
                request,
                fixture.TransactionPath);

        Assert.True(journal.IsSuccess);

        SafeChangeJournalResult firstRecovery =
            await SafeChangeTransactionJournal.RecoverAsync(
                request,
                fixture.TransactionPath);

        SafeChangeJournalResult secondRecovery =
            await SafeChangeTransactionJournal.RecoverAsync(
                request,
                fixture.TransactionPath);

        Assert.True(firstRecovery.IsSuccess);
        Assert.True(firstRecovery.WasRecovered);
        Assert.True(secondRecovery.IsSuccess);
        Assert.False(secondRecovery.WasRecovered);
        Assert.Equal(
            "before",
            fixture.Read("src/Existing.cs"));
    }

    [Fact]
    public async Task Tampered_manifest_fails_closed()
    {
        using var fixture =
            new Fixture();

        SafeChangeApplicationRequest request =
            fixture.Request(
                fixture.Create(
                    "src/New.cs",
                    "new"));

        fixture.PrepareJournalStages(
            request);

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

        SafeChangeJournalResult recovery =
            await SafeChangeTransactionJournal.RecoverAsync(
                request,
                fixture.TransactionPath);

        Assert.False(recovery.IsSuccess);
        Assert.Equal(
            SafeChangeApplicationFailureKind.AtomicityFailure,
            recovery.FailureKind);
        Assert.Equal(
            "SAFE_CHANGE_RECOVERY_MANIFEST_INVALID",
            recovery.ErrorCode);
        Assert.True(
            Directory.Exists(
                fixture.TransactionPath));
        Assert.False(
            File.Exists(
                fixture.PathOf("src/New.cs")));
    }

    private sealed class Fixture :
        IDisposable
    {
        public Fixture()
        {
            Root =
                Path.Combine(
                    Path.GetTempPath(),
                    "kronxy-journal-tests",
                    Guid.NewGuid().ToString("N"));

            WorkspacePath =
                Path.Combine(
                    Root,
                    "workspace");

            RepositoryPath =
                Path.Combine(
                    WorkspacePath,
                    "repository");

            Directory.CreateDirectory(
                RepositoryPath);

            JobId =
                Guid.NewGuid();

            RunId =
                Guid.NewGuid();

            Repository =
                new RepositoryWorktreeHandle(
                    JobId,
                    "KRX-RECOVERY",
                    WorkspacePath,
                    RepositoryPath,
                    "kronxy/jobs/recovery",
                    new string('a', 40),
                    RepositoryWorktreeOperationKind.Existing);
        }

        public string Root { get; }

        public string WorkspacePath { get; }

        public string RepositoryPath { get; }

        public Guid JobId { get; }

        public Guid RunId { get; }

        public RepositoryWorktreeHandle Repository { get; }

        public string TransactionPath =>
            SafeChangeProposalLineage.GetTransactionPath(
                Request(Create(
                    "placeholder.cs",
                    "placeholder")));

        public string StagePath(
            int index)
        {
            return Path.Combine(
                TransactionPath,
                $"stage-{index:D4}.tmp");
        }

        public string BackupPath(
            int index)
        {
            return Path.Combine(
                TransactionPath,
                $"backup-{index:D4}.bin");
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

        public ValidatedDeveloperChange Create(
            string relativePath,
            string content)
        {
            return Change(
                DeveloperChangeOperationType.CreateFile,
                relativePath,
                string.Empty,
                content);
        }

        public ValidatedDeveloperChange Replace(
            string relativePath,
            string before,
            string after)
        {
            return Change(
                DeveloperChangeOperationType.ReplaceFile,
                relativePath,
                Hash(before),
                after);
        }

        public SafeChangeApplicationRequest Request(
            params ValidatedDeveloperChange[] changes)
        {
            long totalBytes =
                changes.Sum(
                    change =>
                        (long)change.ContentBytes);

            return new SafeChangeApplicationRequest
            {
                JobId = JobId,
                RunId = RunId,
                AttemptCount = 1,
                Repository = Repository,
                Proposal =
                    new ValidatedDeveloperProposal(
                        "Recovery test proposal.",
                        changes,
                        [],
                        [],
                        totalBytes,
                        totalBytes),
                CorrelationId =
                    "recovery-test",
                ProposalLineageId =
                    "developer:recovery-test"
            };
        }

        public void PrepareJournalStages(
            SafeChangeApplicationRequest request)
        {
            Directory.CreateDirectory(
                TransactionPath);

            for (
                int index = 0;
                index < request.Proposal.Changes.Count;
                index++)
            {
                File.WriteAllText(
                    StagePath(index),
                    request.Proposal.Changes[index].Content,
                    new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier: false));
            }
        }

        public void Write(
            string relativePath,
            string content)
        {
            string path =
                PathOf(
                    relativePath);

            Directory.CreateDirectory(
                Path.GetDirectoryName(path)!);

            File.WriteAllText(
                path,
                content);
        }

        public string Read(
            string relativePath)
        {
            return File.ReadAllText(
                PathOf(relativePath));
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(
                    Root,
                    recursive: true);
            }
        }

        private static ValidatedDeveloperChange Change(
            DeveloperChangeOperationType operation,
            string relativePath,
            string expectedHash,
            string content)
        {
            return new ValidatedDeveloperChange(
                operation,
                relativePath,
                "Apply deterministic recovery test change.",
                content,
                expectedHash,
                Encoding.UTF8.GetByteCount(content));
        }

        private static string Hash(
            string content)
        {
            return Convert.ToHexString(
                    SHA256.HashData(
                        Encoding.UTF8.GetBytes(content)))
                .ToLowerInvariant();
        }
    }
}

using Kronxy.Context.Git;
using Kronxy.Context.Processes;
using Xunit;

namespace Kronxy.Context.Tests.Git;

public sealed class GitClientTests
{
    private const string ObjectId = "0123456789012345678901234567890123456789";
    private readonly string existingPath = Path.GetTempPath();

    [Fact]
    public async Task DiscoverRoot_InvokesGitWithHardenedSeparatedArguments()
    {
        var fake = new FakeProcessRunner();
        fake.Enqueue(FakeProcessRunner.Success(existingPath + Environment.NewLine));

        var root = await new GitClient(fake).DiscoverRootAsync(existingPath);

        Assert.Equal(Path.GetFullPath(existingPath), root);
        var request = Assert.Single(fake.Requests);
        Assert.Equal("git", request.FileName);
        Assert.Equal(Path.GetFullPath(existingPath), request.WorkingDirectory);
        Assert.Equal(
            ["--no-pager", "--no-optional-locks", "-c", "color.ui=false", "-c", "core.pager=cat",
             "-c", "core.fsmonitor=false", "-c", "credential.interactive=never", "rev-parse", "--show-toplevel"],
            request.Arguments);
        Assert.Equal("0", request.EnvironmentVariables["GIT_TERMINAL_PROMPT"]);
        Assert.Equal("Never", request.EnvironmentVariables["GCM_INTERACTIVE"]);
        Assert.Equal("0", request.EnvironmentVariables["GIT_OPTIONAL_LOCKS"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-option")]
    [InlineData("HEAD\0bad")]
    [InlineData("HEAD\nbad")]
    [InlineData("HEAD\rbad")]
    public async Task ResolveCommit_RejectsInvalidReferenceBeforeProcessExecution(string reference)
    {
        var fake = new FakeProcessRunner();
        var exception = await Assert.ThrowsAsync<GitClientException>(() =>
            new GitClient(fake).ResolveCommitAsync(existingPath, reference));

        Assert.Equal(GitErrorKind.InvalidReference, exception.Kind);
        Assert.Empty(fake.Requests);
    }

    [Fact]
    public async Task ResolveCommit_UsesEndOfOptionsAndPeelsOnlyValidatedObjectId()
    {
        var fake = new FakeProcessRunner();
        EnqueueRoot(fake);
        fake.Enqueue(FakeProcessRunner.Success(ObjectId + "\n"));
        fake.Enqueue(FakeProcessRunner.Success(ObjectId + "\n"));

        var resolved = await new GitClient(fake).ResolveCommitAsync(existingPath, "feature/name");

        Assert.Equal(ObjectId, resolved);
        Assert.Equal(["rev-parse", "--verify", "--end-of-options", "feature/name"], fake.Requests[1].Arguments.TakeLast(4));
        Assert.Equal(["rev-parse", "--verify", "--end-of-options", ObjectId + "^{commit}"], fake.Requests[2].Arguments.TakeLast(4));
    }

    [Fact]
    public async Task GetWorkingTreeStatus_UsesPorcelainV2NulOutput()
    {
        var fake = new FakeProcessRunner();
        EnqueueRoot(fake);
        fake.Enqueue(FakeProcessRunner.Success("? ruta con espacios.txt\0"));

        var status = await new GitClient(fake).GetWorkingTreeStatusAsync(existingPath);

        Assert.True(status.HasUntrackedFiles);
        Assert.Equal(["status", "--porcelain=v2", "-z", "--untracked-files=all"], fake.Requests[1].Arguments.TakeLast(4));
    }

    [Fact]
    public async Task GetRepositoryInfo_RepresentsDetachedHead()
    {
        var fake = new FakeProcessRunner();
        EnqueueRoot(fake);
        EnqueueResolvedCommit(fake);
        fake.Enqueue(FakeProcessRunner.Failure(1));

        var info = await new GitClient(fake).GetRepositoryInfoAsync(existingPath);

        Assert.True(info.IsHeadDetached);
        Assert.Null(info.Branch);
        Assert.Equal(ObjectId, info.HeadCommit);
    }

    [Fact]
    public async Task GetRepositoryInfo_SymbolicRefExitZeroReturnsBranch()
    {
        var fake = new FakeProcessRunner();
        EnqueueRoot(fake);
        EnqueueResolvedCommit(fake);
        fake.Enqueue(FakeProcessRunner.Success("main\n"));

        var info = await new GitClient(fake).GetRepositoryInfoAsync(existingPath);

        Assert.False(info.IsHeadDetached);
        Assert.Equal("main", info.Branch);
    }

    [Fact]
    public async Task GetRepositoryInfo_SymbolicRefUnexpectedExitIsError()
    {
        var fake = new FakeProcessRunner();
        EnqueueRoot(fake);
        EnqueueResolvedCommit(fake);
        fake.Enqueue(FakeProcessRunner.Failure(2));

        var exception = await Assert.ThrowsAsync<GitClientException>(() =>
            new GitClient(fake).GetRepositoryInfoAsync(existingPath));

        Assert.Equal(GitErrorKind.CommandFailed, exception.Kind);
    }

    [Fact]
    public async Task IsAncestor_UsesOnlyResolvedObjectIds()
    {
        var fake = new FakeProcessRunner();
        EnqueueRoot(fake);
        EnqueueResolvedCommit(fake);
        EnqueueResolvedCommit(fake);
        fake.Enqueue(FakeProcessRunner.Success());

        Assert.True(await new GitClient(fake).IsAncestorAsync(existingPath, "from", "to"));
        Assert.Equal(["merge-base", "--is-ancestor", ObjectId, ObjectId], fake.Requests[^1].Arguments.TakeLast(4));
    }

    [Fact]
    public async Task GetChanges_UsesResolvedIdsEndMarkerRenameDetectionAndNoExternalDiff()
    {
        var fake = new FakeProcessRunner();
        EnqueueRoot(fake);
        EnqueueResolvedCommit(fake);
        EnqueueResolvedCommit(fake);
        fake.Enqueue(FakeProcessRunner.Success("M\0file.txt\0"));

        var changes = await new GitClient(fake).GetChangesAsync(existingPath, "from", "to");

        Assert.Single(changes);
        Assert.Equal(
            ["diff", "--name-status", "-z", "--find-renames", "--no-ext-diff", "--no-textconv", ObjectId, ObjectId, "--"],
            fake.Requests[^1].Arguments.TakeLast(9));
    }

    [Fact]
    public async Task IsAncestor_UnexpectedExitIsError()
    {
        var fake = new FakeProcessRunner();
        EnqueueRoot(fake);
        EnqueueResolvedCommit(fake);
        EnqueueResolvedCommit(fake);
        fake.Enqueue(FakeProcessRunner.Failure(2));

        var exception = await Assert.ThrowsAsync<GitClientException>(() =>
            new GitClient(fake).IsAncestorAsync(existingPath, "from", "to"));

        Assert.Equal(GitErrorKind.CommandFailed, exception.Kind);
    }

    [Theory]
    [InlineData("0123456789012345678901234567890123456789 extra\n")]
    [InlineData("012345678901234567890123456789012345678g\n")]
    [InlineData("0123456789012345678901234567890123456789\nsecond\n")]
    public async Task ResolveCommit_RejectsMalformedObjectIdOutput(string output)
    {
        var fake = new FakeProcessRunner();
        EnqueueRoot(fake);
        fake.Enqueue(FakeProcessRunner.Success(output));

        var exception = await Assert.ThrowsAsync<GitClientException>(() =>
            new GitClient(fake).ResolveCommitAsync(existingPath, "HEAD"));

        Assert.Equal(GitErrorKind.MalformedOutput, exception.Kind);
    }

    [Fact]
    public async Task ResolveCommit_AcceptsSha256LengthObjectId()
    {
        var objectId = new string('a', 64);
        var fake = new FakeProcessRunner();
        EnqueueRoot(fake);
        fake.Enqueue(FakeProcessRunner.Success(objectId + "\n"));
        fake.Enqueue(FakeProcessRunner.Success(objectId + "\n"));

        Assert.Equal(objectId, await new GitClient(fake).ResolveCommitAsync(existingPath, "HEAD"));
    }

    [Fact]
    public async Task NonexistentReferenceProducesTypedSanitizedError()
    {
        const string sensitiveReference = "secret-reference-value";
        var fake = new FakeProcessRunner();
        EnqueueRoot(fake);
        fake.Enqueue(FakeProcessRunner.Failure());

        var exception = await Assert.ThrowsAsync<GitClientException>(() =>
            new GitClient(fake).ResolveCommitAsync(existingPath, sensitiveReference));

        Assert.Equal(GitErrorKind.ReferenceNotFound, exception.Kind);
        Assert.DoesNotContain(sensitiveReference, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ProcessTerminationCause.TimedOut, GitErrorKind.TimedOut)]
    [InlineData(ProcessTerminationCause.StandardOutputLimitExceeded, GitErrorKind.OutputLimitExceeded)]
    [InlineData(ProcessTerminationCause.StandardErrorLimitExceeded, GitErrorKind.OutputLimitExceeded)]
    public async Task ProcessTerminationMapsToTypedGitError(ProcessTerminationCause cause, GitErrorKind kind)
    {
        var fake = new FakeProcessRunner();
        fake.Enqueue(new ProcessResult { TerminationCause = cause });

        var exception = await Assert.ThrowsAsync<GitClientException>(() =>
            new GitClient(fake).DiscoverRootAsync(existingPath));
        Assert.Equal(kind, exception.Kind);
    }

    [Fact]
    public void PublicSurfaceDoesNotExposeArbitraryGitExecution()
    {
        var methodNames = typeof(IGitClient).GetMethods().Select(method => method.Name).ToArray();
        Assert.DoesNotContain(methodNames, name => name.Contains("RunGit", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, name => name.Contains("ExecuteGit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MissingPathIsRejectedBeforeProcessExecution()
    {
        var fake = new FakeProcessRunner();
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var exception = await Assert.ThrowsAsync<GitClientException>(() =>
            new GitClient(fake).DiscoverRootAsync(missingPath));

        Assert.Equal(GitErrorKind.PathNotFound, exception.Kind);
        Assert.Empty(fake.Requests);
    }

    [Fact]
    public async Task ProcessStartFailureIsReportedAsGitUnavailableWithoutArguments()
    {
        const string sensitive = "sensitive-inner-argument";
        var fake = new FakeProcessRunner();
        fake.Enqueue(new ProcessRunnerException("technical failure"));

        var exception = await Assert.ThrowsAsync<GitClientException>(() =>
            new GitClient(fake).DiscoverRootAsync(existingPath));

        Assert.Equal(GitErrorKind.GitUnavailable, exception.Kind);
        Assert.DoesNotContain(sensitive, exception.ToString(), StringComparison.Ordinal);
    }

    private void EnqueueRoot(FakeProcessRunner fake) =>
        fake.Enqueue(FakeProcessRunner.Success(existingPath + Environment.NewLine));

    private void EnqueueResolvedCommit(FakeProcessRunner fake)
    {
        EnqueueRoot(fake);
        fake.Enqueue(FakeProcessRunner.Success(ObjectId + "\n"));
        fake.Enqueue(FakeProcessRunner.Success(ObjectId + "\n"));
    }
}

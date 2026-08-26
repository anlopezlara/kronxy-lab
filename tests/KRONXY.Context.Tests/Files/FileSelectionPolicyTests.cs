using Kronxy.Context.Configuration;
using Kronxy.Context.Files;
using Kronxy.Context.Git;
using Xunit;

namespace Kronxy.Context.Tests.Files;

public sealed class FileSelectionPolicyTests
{
    private readonly FileSelectionPolicy policy = new();
    private readonly RepositoryInventoryLimits limits = RepositoryInventoryLimits.FromOptions(new ContextOptions(), 1_000_000);

    [Theory]
    [InlineData("bin/tracked.cs")]
    [InlineData("node_modules/pkg/index.js")]
    [InlineData(".kronxy-context/packages/out.txt")]
    [InlineData("TestResults/result.xml")]
    public void ExcludesProhibitedDirectories(string path) =>
        Assert.Equal(FileExclusionReason.ProhibitedDirectory, Evaluate(path).Reason);

    [Fact]
    public void DirectoryCaseComparisonMatchesPlatform()
    {
        Assert.Equal(OperatingSystem.IsWindows(), !Evaluate("SRC/OBJ/file.cs").Include);
    }

    [Theory]
    [InlineData(".env")]
    [InlineData(".env.local")]
    [InlineData("server.pfx")]
    [InlineData("private.key")]
    [InlineData("credentials.json")]
    [InlineData("settings.user")]
    [InlineData("id_rsa")]
    [InlineData("id_ed25519")]
    [InlineData(".npmrc")]
    [InlineData(".pypirc")]
    public void ExcludesSensitiveNames(string path) =>
        Assert.Equal(FileExclusionReason.SensitiveName, Evaluate(path).Reason);

    [Theory]
    [InlineData("image.PNG")]
    [InlineData("manual.pdf")]
    [InlineData("office.docx")]
    [InlineData("assembly.dll")]
    [InlineData("archive.zip")]
    public void ExcludesKnownBinaryExtensions(string path) =>
        Assert.Equal(FileExclusionReason.BinaryExtension, Evaluate(path).Reason);

    [Theory]
    [InlineData(".env.example")]
    [InlineData(".gitignore")]
    [InlineData("AGENTS.md")]
    [InlineData("project.sln")]
    [InlineData("project.csproj")]
    [InlineData("source.cs")]
    [InlineData("migration.sql")]
    [InlineData("README.md")]
    [InlineData("appsettings.Production.json")]
    [InlineData("Dockerfile")]
    public void AllowsImportantTextCandidates(string path) => Assert.True(Evaluate(path).Include);

    [Theory]
    [InlineData(GitIndexEntryType.SymbolicLink, FileExclusionReason.SymbolicLink)]
    [InlineData(GitIndexEntryType.GitLink, FileExclusionReason.GitLink)]
    [InlineData(GitIndexEntryType.Unknown, FileExclusionReason.UnknownGitType)]
    public void ExcludesUnsafeGitTypes(GitIndexEntryType type, FileExclusionReason reason) =>
        Assert.Equal(reason, policy.Evaluate(Candidate("file", type), limits, ".kronxy-context/packages").Reason);

    [Fact]
    public void ExcludesConflict() =>
        Assert.Equal(FileExclusionReason.Conflict,
            policy.Evaluate(Candidate("file", stage: 2), limits, ".kronxy-context/packages").Reason);

    private FileSelectionDecision Evaluate(string path) =>
        policy.Evaluate(Candidate(path), limits, ".kronxy-context/packages");
    private static RepositoryFileCandidate Candidate(
        string path,
        GitIndexEntryType type = GitIndexEntryType.RegularFile,
        int stage = 0) => new()
        {
            LogicalPath = path,
            Origin = RepositoryFileOrigin.Tracked,
            GitEntryType = type,
            Stage = stage
        };
}

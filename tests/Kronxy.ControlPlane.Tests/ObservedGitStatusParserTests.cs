using Kronxy.Application.Repositories;
using Kronxy.Infrastructure.Git;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class ObservedGitStatusParserTests
{
    [Fact]
    public void Parses_complete_typed_change_set_without_human_quoting()
    {
        var result = ObservedGitStatusParser.Parse(
            "?? new file ñ.txt\0 M tracked.txt\0D  removed.txt\0");

        Assert.True(result.IsSuccess);
        Assert.Collection(result.Value!,
            value => Assert.Equal(new(ObservedRepositoryChangeKind.Created, "new file ñ.txt"), value),
            value => Assert.Equal(new(ObservedRepositoryChangeKind.Modified, "tracked.txt"), value),
            value => Assert.Equal(new(ObservedRepositoryChangeKind.Deleted, "removed.txt"), value));
    }

    [Theory]
    [InlineData("R  old\0new\0", "REPOSITORY_OBSERVED_CHANGE_UNSUPPORTED")]
    [InlineData("?? same\0?? same\0", "REPOSITORY_OBSERVED_PATH_INVALID")]
    [InlineData("?? ../escape\0", "REPOSITORY_OBSERVED_PATH_INVALID")]
    [InlineData("X  unknown\0", "REPOSITORY_OBSERVED_CHANGE_UNSUPPORTED")]
    [InlineData("?? incomplete", "REPOSITORY_OBSERVED_STATUS_TRUNCATED")]
    public void Fails_closed_for_unsupported_or_invalid_records(
        string porcelain,
        string expectedCode)
    {
        var result = ObservedGitStatusParser.Parse(porcelain);
        Assert.False(result.IsSuccess);
        Assert.Equal(RepositoryFailureKind.UnsupportedObservedChange, result.FailureKind);
        Assert.Equal(expectedCode, result.ErrorCode);
    }
}

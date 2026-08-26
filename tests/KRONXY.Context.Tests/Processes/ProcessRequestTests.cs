using Kronxy.Context.Processes;
using Xunit;

namespace Kronxy.Context.Tests.Processes;

public sealed class ProcessRequestTests
{
    [Fact]
    public void Defaults_InitializeCollectionsAndLimits()
    {
        var request = ValidRequest();

        Assert.Empty(request.Arguments);
        Assert.Empty(request.EnvironmentVariables);
        Assert.True(request.Timeout > TimeSpan.Zero);
        Assert.True(request.StandardOutputLimitBytes > 0);
        Assert.True(request.StandardErrorLimitBytes > 0);
        Assert.DoesNotContain(request.GetType().GetProperties(), property => property.Name == "ArgumentsLine");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidOutputLimits_Throws(int limit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            (ValidRequest() with { StandardOutputLimitBytes = limit }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            (ValidRequest() with { StandardErrorLimitBytes = limit }).Validate());
    }

    [Fact]
    public void Validate_MissingWorkingDirectory_Throws() =>
        Assert.Throws<DirectoryNotFoundException>(() =>
            (ValidRequest() with { WorkingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")) }).Validate());

    [Fact]
    public void Validate_NulArgument_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            (ValidRequest() with { Arguments = ["safe", "bad\0value"] }).Validate());

    [Fact]
    public void Validate_TimeoutUnsupportedByCancellationTokenSource_ThrowsBeforeExecution() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            (ValidRequest() with { Timeout = TimeSpan.FromDays(100) }).Validate());

    private static ProcessRequest ValidRequest() => new()
    {
        FileName = "tool",
        WorkingDirectory = Path.GetTempPath()
    };
}

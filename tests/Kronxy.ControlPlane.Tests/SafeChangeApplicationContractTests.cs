using Kronxy.Application.Execution;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class SafeChangeApplicationContractTests
{
    [Fact]
    public void Success_requires_a_valid_report()
    {
        Guid jobId =
            Guid.NewGuid();

        Guid runId =
            Guid.NewGuid();

        DateTime started =
            DateTime.UtcNow;

        var report =
            new SafeChangeApplicationReport(
                jobId,
                runId,
                [
                    new AppliedFileChange(
                        DeveloperChangeOperationType.CreateFile,
                        "src/NewFile.cs",
                        null,
                        new string('a', 64),
                        10)
                ],
                10,
                started,
                started.AddSeconds(1));

        SafeChangeApplicationResult result =
            SafeChangeApplicationResult.Success(
                report);

        Assert.True(report.IsSuccess);
        Assert.True(result.IsSuccess);
        Assert.Same(report, result.Report);
        Assert.Equal(
            SafeChangeApplicationFailureKind.None,
            result.FailureKind);
        Assert.Equal(
            string.Empty,
            result.ErrorCode);
    }

    [Fact]
    public void Failure_never_exposes_a_report()
    {
        SafeChangeApplicationResult result =
            SafeChangeApplicationResult.Failure(
                SafeChangeApplicationFailureKind
                    .PreconditionFailed,
                "SAFE_CHANGE_PRECONDITION_FAILED");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Report);
        Assert.Equal(
            SafeChangeApplicationFailureKind
                .PreconditionFailed,
            result.FailureKind);
        Assert.Equal(
            "SAFE_CHANGE_PRECONDITION_FAILED",
            result.ErrorCode);
    }
}

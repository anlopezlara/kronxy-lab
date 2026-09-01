using Kronxy.Application.Jobs;
using Xunit;

namespace Kronxy.ControlPlane.Tests;

public sealed class JobRunIdProviderTests
{
    private readonly IJobRunIdProvider provider =
        new DeterministicJobRunIdProvider();

    [Fact]
    public void Same_job_and_attempt_produce_same_run_id()
    {
        Guid jobId =
            Guid.Parse(
                "11111111-2222-3333-4444-555555555555");

        Guid first =
            provider.Create(
                jobId,
                1);

        Guid second =
            provider.Create(
                jobId,
                1);

        Assert.Equal(
            first,
            second);

        Assert.NotEqual(
            Guid.Empty,
            first);
    }

    [Fact]
    public void Different_attempt_produces_different_run_id()
    {
        Guid jobId =
            Guid.Parse(
                "11111111-2222-3333-4444-555555555555");

        Guid first =
            provider.Create(
                jobId,
                1);

        Guid second =
            provider.Create(
                jobId,
                2);

        Assert.NotEqual(
            first,
            second);
    }

    [Fact]
    public void Different_job_produces_different_run_id()
    {
        Guid first =
            provider.Create(
                Guid.Parse(
                    "11111111-2222-3333-4444-555555555555"),
                1);

        Guid second =
            provider.Create(
                Guid.Parse(
                    "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                1);

        Assert.NotEqual(
            first,
            second);
    }

    [Fact]
    public void Invalid_identity_fails_closed()
    {
        Assert.Throws<ArgumentException>(
            () =>
                provider.Create(
                    Guid.Empty,
                    1));

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                provider.Create(
                    Guid.NewGuid(),
                    0));
    }
}

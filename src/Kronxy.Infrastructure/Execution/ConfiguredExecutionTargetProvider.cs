using Kronxy.Application.Execution;

namespace Kronxy.Infrastructure.Execution;

public sealed class ConfiguredExecutionTargetProvider :
    IExecutionTargetProvider
{
    private readonly ExecutionPlaneOptions options;

    public ConfiguredExecutionTargetProvider(
        ExecutionPlaneOptions options)
    {
        this.options =
            options ??
            throw new ArgumentNullException(
                nameof(options));
    }

    public string DotnetTarget =>
        options.DotnetTarget;
}

namespace Kronxy.Context.Processes;

public sealed record ProcessResult
{
    public int? ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public ProcessTerminationCause TerminationCause { get; init; }
    public bool TimedOut => TerminationCause == ProcessTerminationCause.TimedOut;
    public bool OutputLimitExceeded =>
        TerminationCause is ProcessTerminationCause.StandardOutputLimitExceeded or
            ProcessTerminationCause.StandardErrorLimitExceeded;
}

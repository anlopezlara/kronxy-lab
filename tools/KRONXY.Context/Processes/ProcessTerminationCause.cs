namespace Kronxy.Context.Processes;

public enum ProcessTerminationCause
{
    Completed,
    NonZeroExitCode,
    TimedOut,
    StandardOutputLimitExceeded,
    StandardErrorLimitExceeded
}

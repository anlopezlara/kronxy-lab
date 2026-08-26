namespace Kronxy.Context.Processes;

public sealed class ProcessRunnerException : Exception
{
    public ProcessRunnerException(string message)
        : base(message)
    {
    }
}

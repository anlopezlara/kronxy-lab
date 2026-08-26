using Kronxy.Context.Processes;

namespace Kronxy.Context.Tests.Git;

internal sealed class FakeProcessRunner : IProcessRunner
{
    private readonly Queue<Func<ProcessRequest, ProcessResult>> responses = new();

    public List<ProcessRequest> Requests { get; } = [];

    public void Enqueue(ProcessResult result) => responses.Enqueue(_ => result);
    public void Enqueue(Exception exception) => responses.Enqueue(_ => throw exception);

    public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        if (responses.Count == 0)
        {
            throw new InvalidOperationException("No se configuró una respuesta para el proceso simulado.");
        }

        return Task.FromResult(responses.Dequeue()(request));
    }

    public static ProcessResult Success(string output = "") => new()
    {
        ExitCode = 0,
        StandardOutput = output,
        TerminationCause = ProcessTerminationCause.Completed
    };

    public static ProcessResult Failure(int exitCode = 1) => new()
    {
        ExitCode = exitCode,
        TerminationCause = ProcessTerminationCause.NonZeroExitCode
    };
}

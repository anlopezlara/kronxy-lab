namespace Kronxy.Context.Commands;

public sealed record CommandParseResult
{
    private CommandParseResult()
    {
    }

    public bool Success { get; private init; }
    public CommandRequest? Request { get; private init; }
    public string? ErrorMessage { get; private init; }
    public bool HelpRequested { get; private init; }
    public bool VersionRequested { get; private init; }

    public static CommandParseResult Parsed(CommandRequest request) =>
        new() { Success = true, Request = request };

    public static CommandParseResult Error(string message) =>
        new() { ErrorMessage = message };

    public static CommandParseResult Help() =>
        new() { Success = true, HelpRequested = true };

    public static CommandParseResult Version() =>
        new() { Success = true, VersionRequested = true };
}

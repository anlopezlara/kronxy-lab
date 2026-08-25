using Kronxy.Context.Models;

namespace Kronxy.Context.Commands;

public sealed record CommandRequest
{
    public required ContextCommand Command { get; init; }
    public string? Repository { get; init; }
    public string? Output { get; init; }
    public string? WorkItem { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
    public string? Ref { get; init; }
    public string? Package { get; init; }
    public string? ErrorFile { get; init; }
    public string? ErrorText { get; init; }
    public PackageStability? Stability { get; init; }
}

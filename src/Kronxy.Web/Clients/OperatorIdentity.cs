namespace Kronxy.Web.Clients;

public interface IOperatorIdentity
{
    string Actor { get; }
    string NewCorrelationId(string operation);
}

public sealed class ConfiguredOperatorIdentity(IConfiguration configuration) : IOperatorIdentity
{
    public string Actor { get; } = configuration["KronxyOperator:Actor"]?.Trim() is { Length: > 0 } actor
        ? actor
        : "LocalOperator";

    public string NewCorrelationId(string operation) =>
        $"web-{Normalize(operation)}-{Guid.NewGuid():N}";

    private static string Normalize(string value) => string.Concat(value
        .ToLowerInvariant()
        .Select(character => char.IsLetterOrDigit(character) ? character : '-'));
}

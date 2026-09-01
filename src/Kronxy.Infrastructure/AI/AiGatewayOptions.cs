namespace Kronxy.Infrastructure.AI;

public sealed record AiGatewayOptions
{
    public const string SectionName =
        "AI";

    public required string Provider
    {
        get;
        init;
    }

    public required string Endpoint
    {
        get;
        init;
    }

    public required IReadOnlyDictionary<string, string>
        Models
    {
        get;
        init;
    }

    public int MaxConcurrentInferences
    {
        get;
        init;
    }

    public TimeSpan ConnectionTimeout
    {
        get;
        init;
    }

    public TimeSpan InferenceTimeout
    {
        get;
        init;
    }

    public TimeSpan QueueWaitTimeout
    {
        get;
        init;
    }

    public int MaxOutputTokens
    {
        get;
        init;
    }

    public int MaxInputCharacters
    {
        get;
        init;
    }

    public int MaxResponseBytes
    {
        get;
        init;
    }

    public void Validate()
    {
        if (!string.Equals(
                Provider,
                "Ollama",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "AI Provider is invalid.");
        }

        if (!Uri.TryCreate(
                Endpoint,
                UriKind.Absolute,
                out Uri? endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                "AI Endpoint is invalid.");
        }

        if (Models is null ||
            Models.Count == 0)
        {
            throw new InvalidOperationException(
                "AI Models configuration is required.");
        }

        string[] requiredModels =
        [
            "CodingFast",
            "CodingQuality",
            "General"
        ];

        foreach (string logicalModel in requiredModels)
        {
            if (!Models.TryGetValue(
                    logicalModel,
                    out string? physicalModel) ||
                string.IsNullOrWhiteSpace(
                    physicalModel))
            {
                throw new InvalidOperationException(
                    $"AI model mapping '{logicalModel}' is invalid.");
            }

            if (physicalModel.IndexOfAny(
                    ['\0', '\r', '\n']) >= 0)
            {
                throw new InvalidOperationException(
                    $"AI model mapping '{logicalModel}' is invalid.");
            }
        }

        if (MaxConcurrentInferences <= 0)
        {
            throw new InvalidOperationException(
                "AI MaxConcurrentInferences must be greater than zero.");
        }

        ValidatePositiveTimeout(
            ConnectionTimeout,
            nameof(ConnectionTimeout));

        ValidatePositiveTimeout(
            InferenceTimeout,
            nameof(InferenceTimeout));

        ValidatePositiveTimeout(
            QueueWaitTimeout,
            nameof(QueueWaitTimeout));

        if (MaxOutputTokens <= 0)
        {
            throw new InvalidOperationException(
                "AI MaxOutputTokens must be greater than zero.");
        }

        if (MaxInputCharacters <= 0)
        {
            throw new InvalidOperationException(
                "AI MaxInputCharacters must be greater than zero.");
        }

        if (MaxResponseBytes <= 0)
        {
            throw new InvalidOperationException(
                "AI MaxResponseBytes must be greater than zero.");
        }
    }

    private static void ValidatePositiveTimeout(
        TimeSpan value,
        string name)
    {
        if (value <= TimeSpan.Zero ||
            value == Timeout.InfiniteTimeSpan)
        {
            throw new InvalidOperationException(
                $"AI {name} must be finite and greater than zero.");
        }
    }
}

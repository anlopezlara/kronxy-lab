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
    } = TimeSpan.FromMinutes(2);

    public TimeSpan PlanningInferenceTimeout
    {
        get;
        init;
    } = TimeSpan.FromMinutes(3);

    public TimeSpan DeveloperInferenceTimeout
    {
        get;
        init;
    } = TimeSpan.FromMinutes(3);

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

    public int DeveloperMaxOutputTokens
    {
        get;
        init;
    } = 8_192;

    public int DeveloperMinimumOutputTokens { get; init; } = 2_048;

    public int DeveloperContextWindowTokens { get; init; } = 16_384;

    public int DeveloperContextSafetyMarginTokens { get; init; } = 512;

    public int ContextWindowTokens
    {
        get;
        init;
    } = 16_384;

    public int MaxInputCharacters
    {
        get;
        init;
    }

    public int PlanningContextCharacters
    {
        get;
        init;
    } = 48_000;

    public int DeveloperContextCharacters
    {
        get;
        init;
    } = 48_000;

    public int DeveloperFeedbackCharacters
    {
        get;
        init;
    } = 8_192;

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
            PlanningInferenceTimeout,
            nameof(PlanningInferenceTimeout));

        if (PlanningInferenceTimeout < InferenceTimeout)
        {
            throw new InvalidOperationException(
                "AI PlanningInferenceTimeout must be greater than or equal to InferenceTimeout.");
        }

        ValidatePositiveTimeout(
            DeveloperInferenceTimeout,
            nameof(DeveloperInferenceTimeout));

        if (DeveloperInferenceTimeout < InferenceTimeout)
        {
            throw new InvalidOperationException(
                "AI DeveloperInferenceTimeout must be greater than or equal to InferenceTimeout.");
        }

        ValidatePositiveTimeout(
            QueueWaitTimeout,
            nameof(QueueWaitTimeout));

        if (MaxOutputTokens <= 0)
        {
            throw new InvalidOperationException(
                "AI MaxOutputTokens must be greater than zero.");
        }

        if (DeveloperMaxOutputTokens <= 0)
        {
            throw new InvalidOperationException(
                "AI DeveloperMaxOutputTokens must be greater than zero.");
        }

        if (DeveloperMinimumOutputTokens <= 0 ||
            DeveloperMinimumOutputTokens > DeveloperMaxOutputTokens)
        {
            throw new InvalidOperationException(
                "AI DeveloperMinimumOutputTokens is invalid.");
        }

        if (DeveloperContextWindowTokens <= DeveloperMaxOutputTokens ||
            DeveloperContextSafetyMarginTokens <= 0 ||
            DeveloperContextSafetyMarginTokens >= DeveloperContextWindowTokens)
        {
            throw new InvalidOperationException(
                "AI Developer context envelope is invalid.");
        }

        if (ContextWindowTokens <= Math.Max(
                MaxOutputTokens,
                DeveloperMaxOutputTokens))
        {
            throw new InvalidOperationException(
                "AI ContextWindowTokens must be greater than MaxOutputTokens.");
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

using Kronxy.Application.AI;
using Kronxy.Infrastructure.AI;

namespace Kronxy.Infrastructure.Execution;

public static class DeveloperAiRoutingPolicy
{
    public const AiLogicalModel LogicalModel =
        AiLogicalModel.CodingQuality;

    public static DeveloperAiCapability GetCapability(
        AiGatewayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new DeveloperAiCapability(
            LogicalModel,
            options.DeveloperContextWindowTokens,
            options.DeveloperMaxOutputTokens,
            options.DeveloperMinimumOutputTokens,
            options.DeveloperContextSafetyMarginTokens,
            AiInferenceTimeoutPolicy.Resolve(options, LogicalModel));
    }
}

public sealed record DeveloperAiCapability(
    AiLogicalModel LogicalModel,
    int ContextWindowTokens,
    int MaxOutputTokens,
    int MinimumOutputTokens,
    int SafetyMarginTokens,
    TimeSpan InferenceTimeout);

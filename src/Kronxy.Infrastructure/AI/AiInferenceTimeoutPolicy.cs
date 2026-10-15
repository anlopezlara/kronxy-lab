using Kronxy.Application.AI;

namespace Kronxy.Infrastructure.AI;

public static class AiInferenceTimeoutPolicy
{
    public static TimeSpan Resolve(
        AiGatewayOptions options,
        AiLogicalModel logicalModel) =>
        logicalModel switch
        {
            AiLogicalModel.CodingQuality =>
                options.CodingQualityInferenceTimeout,
            _ => options.InferenceTimeout
        };

    public static TimeSpan ResolveOverrideCeiling(
        AiGatewayOptions options,
        AiLogicalModel logicalModel) =>
        logicalModel == AiLogicalModel.CodingQuality
            ? options.CodingQualityInferenceTimeout
            : options.PlanningInferenceTimeout > options.DeveloperInferenceTimeout
                ? options.PlanningInferenceTimeout
                : options.DeveloperInferenceTimeout;
}

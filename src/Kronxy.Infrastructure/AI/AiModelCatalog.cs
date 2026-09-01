using Kronxy.Application.AI;

namespace Kronxy.Infrastructure.AI;

public sealed class AiModelCatalog
{
    private readonly IReadOnlyDictionary<
        AiLogicalModel,
        string> models;

    public AiModelCatalog(
        AiGatewayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Validate();

        models =
            new Dictionary<AiLogicalModel, string>
            {
                [AiLogicalModel.CodingFast] =
                    GetRequired(
                        options,
                        nameof(AiLogicalModel.CodingFast)),

                [AiLogicalModel.CodingQuality] =
                    GetRequired(
                        options,
                        nameof(AiLogicalModel.CodingQuality)),

                [AiLogicalModel.General] =
                    GetRequired(
                        options,
                        nameof(AiLogicalModel.General))
            };
    }

    public bool TryResolve(
        AiLogicalModel logicalModel,
        out string physicalModel)
    {
        return models.TryGetValue(
            logicalModel,
            out physicalModel!);
    }

    public IReadOnlyCollection<string>
        RequiredPhysicalModels =>
        models.Values
            .Distinct(
                StringComparer.Ordinal)
            .ToArray();

    private static string GetRequired(
        AiGatewayOptions options,
        string logicalModel)
    {
        string value =
            options.Models[logicalModel];

        return value.Trim();
    }
}

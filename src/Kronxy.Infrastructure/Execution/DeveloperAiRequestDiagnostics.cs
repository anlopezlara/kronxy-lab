using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kronxy.Application.AI;
using Kronxy.Infrastructure.AI;
using Kronxy.Infrastructure.AI.Ollama;

namespace Kronxy.Infrastructure.Execution;

internal static class DeveloperAiRequestDiagnostics
{
    public static DeveloperAiRequestFingerprint Create(
        DeveloperInitialRequestParts parts,
        DeveloperTargetContext target,
        string physicalModel,
        AiGatewayOptions options) =>
        Create(
            parts.Request,
            new DeveloperPromptParts(
                parts.UserContent, parts.TargetJson, parts.PriorEffects,
                parts.Prefix, parts.OutputRequirements,
                parts.JobRequestCharacters, parts.PlanningContextCharacters),
            target, physicalModel, options);

    public static DeveloperAiRequestFingerprint Create(
        AiRequest request,
        DeveloperPromptParts parts,
        DeveloperTargetContext target,
        string physicalModel,
        AiGatewayOptions options)
    {
        string schema = request.StructuredOutput?.Schema.GetRawText() ??
            string.Empty;
        byte[] serializedRequest = JsonSerializer.SerializeToUtf8Bytes(
            OllamaProvider.CreateRequestPayload(
                request, physicalModel, options),
            OllamaProvider.RequestJsonOptions);

        return new DeveloperAiRequestFingerprint(
            request.SystemInstructions.Length,
            request.UserContent.Length,
            target.CurrentContent?.Length ?? 0,
            parts.PriorEffects.Length,
            schema.Length,
            Encoding.UTF8.GetByteCount(schema),
            request.SystemInstructions.Length + request.UserContent.Length,
            serializedRequest.Length,
            Sha256(request.SystemInstructions),
            Sha256(request.UserContent),
            Sha256(schema),
            Convert.ToHexString(SHA256.HashData(serializedRequest))
                .ToLowerInvariant(),
            request.Generation.ContextWindowTokens,
            request.Generation.MaxOutputTokens,
            request.Generation.Temperature,
            request.StructuredOutput is not null,
            false,
            physicalModel,
            target.RelativePath,
            target.Exists && Encoding.UTF8.GetByteCount(
                target.CurrentContent ?? string.Empty) > 8_192
                ? "LocalizedPatch"
                : target.Exists ? "ReplaceFile" : "CreateFile",
            parts.TargetJson.Length,
            parts.Prefix.Length,
            parts.OutputRequirements.Length,
            parts.JobRequestCharacters,
            parts.PlanningContextCharacters);
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
}

internal sealed record DeveloperAiRequestFingerprint(
    int SystemPromptCharacters,
    int UserPromptCharacters,
    int SourceContentCharacters,
    int PriorTargetEffectCharacters,
    int StructuredSchemaCharacters,
    int StructuredSchemaBytes,
    int TotalMessageCharacters,
    int SerializedRequestBytes,
    string SystemPromptSha256,
    string UserPromptSha256,
    string SchemaSha256,
    string RequestSha256,
    int? NumContext,
    int? NumPredict,
    double? Temperature,
    bool FormatSchemaPresent,
    bool Think,
    string Model,
    string TargetPath,
    string OperationType,
    int TargetSectionCharacters,
    int PrefixCharacters,
    int OutputRequirementsCharacters,
    int JobRequestCharacters,
    int PlanningContextCharacters);

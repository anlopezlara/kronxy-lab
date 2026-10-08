using System.Text.Json;
using Kronxy.Web.Models;

namespace Kronxy.Web.Presentation;

public static class OperatorPresentation
{
    public static string StateClass(string? state) => state switch
    {
        "Completed" => "success",
        "WaitingHuman" or "WaitingAi" or "RetryPending" => "warning",
        "Failed" or "Rejected" or "Cancelled" or "TimedOut" or "Interrupted" => "danger",
        "Created" => "neutral",
        _ => "active"
    };

    public static IReadOnlyList<AllowedActionDto> VisibleActions(IEnumerable<AllowedActionDto> actions) =>
        actions.Where(action => action.Allowed).ToArray();

    public static string ActionLabel(string action) => action switch
    {
        "retryPending" => "Retry Pending",
        "humanReviewApprove" => "Human Review Approve",
        "humanReviewChangesRequired" => "Human Review Changes Required",
        "architectureDecision" => "Architecture Decision",
        "humanCorrection" => "Human Correction",
        "reviewerHumanReviewCorrectionSupersede" => "Reviewer Correction Supersede",
        _ => string.Concat(action.Select((character, index) => index > 0 && char.IsUpper(character)
            ? $" {character}" : character.ToString()))
    };

    public static string SafeArtifactText(ArtifactContentDto artifact)
    {
        string text = System.Text.Encoding.UTF8.GetString(artifact.Content);
        if (!artifact.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase)) return text;
        try { return JsonSerializer.Serialize(JsonDocument.Parse(text), new JsonSerializerOptions { WriteIndented = true }); }
        catch (JsonException) { return text; }
    }
}

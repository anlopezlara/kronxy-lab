using System.Text;
using Kronxy.Application.Jobs;
using Kronxy.Domain.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace Kronxy.Api.Controllers.Jobs;

[ApiController]
[Route("api/jobs")]
public sealed class OperatorJobsController : ControllerBase
{
    private readonly IOperatorJobService service;
    public OperatorJobsController(IOperatorJobService service) => this.service = service;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? externalId = null, [FromQuery] string? state = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1) return BadRequest(new { code = "OPERATOR_PAGING_INVALID" });
        JobState? parsed = null;
        if (!string.IsNullOrWhiteSpace(state))
        {
            if (!Enum.TryParse(state, true, out JobState value) || !Enum.IsDefined(value))
                return BadRequest(new { code = "OPERATOR_JOB_STATE_INVALID" });
            parsed = value;
        }
        return Ok(await service.GetJobsAsync(page, pageSize, externalId, parsed, cancellationToken));
    }

    [HttpGet("{jobId:guid}/history")]
    public async Task<IActionResult> History(Guid jobId, CancellationToken cancellationToken)
    {
        var result = await service.GetHistoryAsync(jobId, cancellationToken);
        return result is null ? NotFound(NotFoundBody()) : Ok(result);
    }

    [HttpGet("{jobId:guid}/artifacts")]
    public async Task<IActionResult> Artifacts(Guid jobId, CancellationToken cancellationToken)
    {
        var result = await service.GetArtifactsAsync(jobId, cancellationToken);
        return result is null ? NotFound(NotFoundBody()) : Ok(result);
    }

    [HttpGet("{jobId:guid}/artifacts/{artifactId:guid}")]
    public async Task<IActionResult> Artifact(Guid jobId, Guid artifactId,
        CancellationToken cancellationToken)
    {
        OperatorArtifactContent? result = await service.GetArtifactAsync(jobId, artifactId, cancellationToken);
        if (result is null) return NotFound(new { code = "ARTIFACT_NOT_FOUND" });
        if (result.Inline)
            return Content(Encoding.UTF8.GetString(result.Content.Span), result.ContentType, Encoding.UTF8);
        return File(result.Content.ToArray(), result.ContentType, $"{result.Metadata.ArtifactId:N}.bin");
    }

    [HttpGet("{jobId:guid}/effective-lineage")]
    public async Task<IActionResult> EffectiveLineage(Guid jobId, CancellationToken cancellationToken)
    {
        Job? job = await service.GetJobAsync(jobId, cancellationToken);
        if (job is null) return NotFound(NotFoundBody());
        OperatorLineage? result = await service.GetEffectiveLineageAsync(jobId, cancellationToken);
        return result is null ? Conflict(new { code = "EFFECTIVE_LINEAGE_NOT_AVAILABLE" }) : Ok(result);
    }

    [HttpGet("{jobId:guid}/allowed-actions")]
    public async Task<IActionResult> AllowedActions(Guid jobId, CancellationToken cancellationToken)
    {
        var result = await service.GetAllowedActionsAsync(jobId, cancellationToken);
        return result is null ? NotFound(NotFoundBody()) : Ok(result);
    }

    private static object NotFoundBody() => new { code = "Job.NotFound", message = "Job not found." };
}

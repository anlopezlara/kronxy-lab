using Kronxy.Application.Jobs;
using Kronxy.Application.Execution;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace Kronxy.Api.Controllers.Jobs;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController : ControllerBase
{
    private readonly IJobService _jobService;
    private readonly IJobOrchestrator _jobOrchestrator;

    public JobsController(
        IJobService jobService,
        IJobOrchestrator jobOrchestrator)
    {
        _jobService =
            jobService ??
            throw new ArgumentNullException(nameof(jobService));

        _jobOrchestrator =
            jobOrchestrator ??
            throw new ArgumentNullException(nameof(jobOrchestrator));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateJobRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.ExternalId) &&
            !JobExternalId.IsValid(request.ExternalId))
        {
            return BadRequest(
                ToErrorResponse(
                    JobApplicationErrors.InvalidExternalId));
        }

        Result<Job> result =
            await _jobService.CreateAsync(
                request.Request,
                request.ExternalId,
                cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(
                ToErrorResponse(result.Error));
        }

        return CreatedAtAction(
            nameof(Get),
            new
            {
                jobId = result.Value.Id
            },
            ToJobResponse(result.Value));
    }

    [HttpGet("{jobId:guid}")]
    public async Task<IActionResult> Get(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        Result<Job> result =
            await _jobService.GetAsync(
                jobId,
                cancellationToken);

        if (result.IsFailure)
        {
            return NotFound(
                ToErrorResponse(result.Error));
        }

        return Ok(
            ToJobResponse(result.Value));
    }

    [HttpPost("{jobId:guid}/advance")]
    public async Task<IActionResult> Advance(
        Guid jobId,
        JobActionRequest request,
        CancellationToken cancellationToken)
    {
        JobOperationResult result =
            await _jobOrchestrator.AdvanceAsync(
                jobId,
                request.Actor,
                request.CorrelationId,
                cancellationToken);

        return ToOperationResponse(result);
    }

    [HttpPost("{jobId:guid}/resume")]
    public async Task<IActionResult> Resume(
        Guid jobId,
        JobReasonActionRequest request,
        CancellationToken cancellationToken)
    {
        JobOperationResult result =
            await _jobOrchestrator.ResumeAsync(
                jobId,
                request.Reason,
                request.Actor,
                request.CorrelationId,
                cancellationToken);

        return ToOperationResponse(result);
    }

    [HttpPost("{jobId:guid}/human-review/changes-required")]
    public async Task<IActionResult> RequestHumanReviewCorrection(
        Guid jobId,
        HumanReviewCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        JobOperationResult result =
            await _jobOrchestrator.RequestHumanReviewCorrectionAsync(
                jobId,
                request,
                cancellationToken);

        return ToOperationResponse(result);
    }

    [HttpPost("{jobId:guid}/human-review/approve")]
    public async Task<IActionResult> ApproveHumanReview(
        Guid jobId,
        JobActionRequest request,
        CancellationToken cancellationToken)
    {
        JobOperationResult result = await _jobOrchestrator
            .ApproveHumanReviewAsync(
                jobId,
                request.Actor,
                request.CorrelationId,
                cancellationToken);

        return ToOperationResponse(result);
    }

    [HttpPost("{jobId:guid}/retry-pending")]
    public async Task<IActionResult> RetryPending(
        Guid jobId,
        JobReasonActionRequest request,
        CancellationToken cancellationToken)
    {
        JobOperationResult result =
            await _jobOrchestrator.MarkRetryPendingAsync(
                jobId,
                request.Reason,
                request.Actor,
                request.CorrelationId,
                cancellationToken);

        return ToOperationResponse(result);
    }

    [HttpPost("{jobId:guid}/reviewer/human-review-correction/supersede")]
    public async Task<IActionResult> SupersedeHumanReviewCorrectionReview(
        Guid jobId,
        JobActionRequest request,
        CancellationToken cancellationToken)
    {
        JobOperationResult result = await _jobOrchestrator
            .SupersedeHumanReviewCorrectionReviewAsync(
                jobId,
                request.Actor,
                request.CorrelationId,
                cancellationToken);

        return ToOperationResponse(result);
    }

    [HttpPost("{jobId:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid jobId,
        JobActionRequest request,
        CancellationToken cancellationToken)
    {
        JobOperationResult result =
            await _jobService.CancelAsync(
                jobId,
                request.Actor,
                request.CorrelationId,
                cancellationToken);

        return ToOperationResponse(result);
    }

    private IActionResult ToOperationResponse(
        JobOperationResult result)
    {
        if (result.IsSuccess)
        {
            return Ok(
                new
                {
                    kind = result.Kind.ToString()
                });
        }

        object error =
            ToErrorResponse(result.Error);

        return result.Kind switch
        {
            JobOperationKind.PermanentFailure =>
                BadRequest(error),

            JobOperationKind.InvalidTransition =>
                Conflict(error),

            JobOperationKind.Cancelled =>
                Conflict(error),

            JobOperationKind.TimedOut =>
                StatusCode(
                    StatusCodes.Status408RequestTimeout,
                    error),

            JobOperationKind.RetryableFailure =>
                StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    error),

            JobOperationKind.OperationalFailure =>
                StatusCode(
                    StatusCodes.Status500InternalServerError,
                    error),

            _ =>
                StatusCode(
                    StatusCodes.Status500InternalServerError,
                    error)
        };
    }

    private static object ToErrorResponse(
        Error error)
    {
        return new
        {
            code = error.Code,
            message = error.Name
        };
    }

    private static object ToJobResponse(
        Job job)
    {
        return new
        {
            id = job.Id,
            externalId = job.ExternalId,
            request = job.Request,
            state = job.State.ToString(),
            resumeState =
                job.ResumeState?.ToString(),
            attemptCount = job.AttemptCount,
            humanReviewRequired = job.State == JobState.WaitingHuman,
            baseRepositoryHead =
                job.BaseRepositoryHead,
            createdOnUtc =
                job.CreatedOnUtc,
            updatedOnUtc =
                job.UpdatedOnUtc,
            completedOnUtc =
                job.CompletedOnUtc,
            lastErrorCode =
                job.LastErrorCode,
            lastErrorMessage =
                job.LastErrorMessage,
            version =
                job.Version,
            transitions =
                job.Transitions.Select(
                    transition =>
                        new
                        {
                            fromState =
                                transition.FromState.ToString(),
                            toState =
                                transition.ToState.ToString(),
                            occurredOnUtc =
                                transition.OccurredOnUtc,
                            reason =
                                transition.Reason,
                            actor =
                                transition.Actor,
                            correlationId =
                                transition.CorrelationId
                        })
        };
    }

    public sealed record CreateJobRequest(
        string Request,
        string? ExternalId);

    public sealed record JobActionRequest(
        string Actor,
        string CorrelationId);

    public sealed record JobReasonActionRequest(
        string Reason,
        string Actor,
        string CorrelationId);
}

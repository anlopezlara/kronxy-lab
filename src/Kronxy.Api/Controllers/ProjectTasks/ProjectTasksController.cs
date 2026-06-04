using Kronxy.Application.ProjectTasks.ActivateProjectTask;
using Kronxy.Application.ProjectTasks.ChangeProjectTaskStatus;
using Kronxy.Application.ProjectTasks.CreateProjectTask;
using Kronxy.Application.ProjectTasks.DeactivateProjectTask;
using Kronxy.Application.ProjectTasks.GetProjectTask;
using Kronxy.Application.ProjectTasks.UpdateProjectTask;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Kronxy.Api.Controllers.ProjectTasks;

[ApiController]
[Route("api/tasks")]
public class ProjectTasksController : ControllerBase
{
    private readonly ISender _sender;

    public ProjectTasksController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask(
        CreateProjectTaskRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateProjectTaskCommand(
            request.ProjectId,
            request.AssignedUserId,
            request.Title,
            request.Description);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetTasks(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetProjectTasksQuery(),
            cancellationToken);

        return Ok(result.Value);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTask(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetProjectTaskQuery(id),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTask(
        Guid id,
        UpdateProjectTaskRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateProjectTaskCommand(
            id,
            request.AssignedUserId,
            request.Title,
            request.Description);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeactivateTask(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new DeactivateProjectTaskCommand(id),
            cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpPut("{id}/activate")]
    public async Task<IActionResult> ActivateTask(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ActivateProjectTaskCommand(id),
            cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        ChangeProjectTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ChangeProjectTaskStatusCommand(
                id,
                request.Status),
            cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }
}
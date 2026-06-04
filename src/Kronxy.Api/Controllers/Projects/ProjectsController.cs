using Kronxy.Application.Projects.ActivateProject;
using Kronxy.Application.Projects.CreateProject;
using Kronxy.Application.Projects.DeactivateProject;
using Kronxy.Application.Projects.GetProject;
using Kronxy.Application.Projects.UpdateProject;
using MediatR;
using Microsoft.AspNetCore.Mvc;
namespace Kronxy.Api.Controllers.Projects;
[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly ISender _sender;
    public ProjectsController(ISender sender)
    {
        _sender = sender;
    }
    [HttpPost]
    public async Task<IActionResult> CreateProject(
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateProjectCommand(
            request.Code,
            request.Name,
            request.Description,
            request.OwnerId,
            request.ProjectTypeId,
            request.ProjectStatusId);
        var result = await _sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }
        return CreatedAtAction(nameof(GetProject), new { id = result.Value }, result.Value);
    }
    [HttpGet]
    public async Task<IActionResult> GetProjects(
        CancellationToken cancellationToken)
    {
        var query = new GetProjectsQuery();
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result.Value);
    }
    [HttpGet("{id}")]
    public async Task<IActionResult> GetProject(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetProjectQuery(id);
        var result = await _sender.Send(query, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProject(
        Guid id,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateProjectCommand(
            id,
            request.Code,
            request.Name,
            request.Description,
            request.OwnerId,
            request.ProjectTypeId,
            request.ProjectStatusId);
        var result = await _sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }
        return NoContent();
    }
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeactivateProject(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeactivateProjectCommand(id);
        var result = await _sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }
        return NoContent();
    }
    [HttpPut("{id}/activate")]
    public async Task<IActionResult> ActivateProject(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new ActivateProjectCommand(id);
        var result = await _sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }
        return NoContent();
    }
}

using Kronxy.Application.Users.CreateUser;
using Kronxy.Application.Users.DeactivateUser;
using Kronxy.Application.Users.GetUser;
using Kronxy.Application.Users.GetUsers;
using Kronxy.Application.Users.UpdateUser;
using Kronxy.Application.Users.ActivateUser;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Kronxy.Api.Controllers.Users;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateUserCommand(
            request.Username,
            request.FirstName,
            request.LastName,
            request.Email,
            request.PhoneNumber,
            request.RoleId);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(Guid id,CancellationToken cancellationToken)
    {
        var query = new GetUserQuery(id);

        var result = await _sender.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var query = new GetUsersQuery();

        var result = await _sender.Send(query, cancellationToken);

        return Ok(result.Value);
    }
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(
    Guid id,
    UpdateUserRequest request,
    CancellationToken cancellationToken)
    {
        var command = new UpdateUserCommand(
            id,
            request.Username,
            request.FirstName,
            request.LastName,
            request.Email,
            request.PhoneNumber,
            request.RoleId);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeactivateUser(
    Guid id,
    CancellationToken cancellationToken)
    {
        var command = new DeactivateUserCommand(id);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return NotFound(result.Error);
        }

        return NoContent();
    }

    [HttpPut("{id}/activate")]
    public async Task<IActionResult> ActivateUser(
    Guid id,
    CancellationToken cancellationToken)
    {
        var command = new ActivateUserCommand(id);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

}
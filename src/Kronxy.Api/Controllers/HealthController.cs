using Kronxy.Application.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace Kronxy.Api.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{
    [HttpGet("/health")]
    public async Task<IActionResult> Get(
        [FromServices] IOperatorHealthService healthService,
        CancellationToken cancellationToken)
    {
        OperatorHealth health = await healthService.CheckAsync(cancellationToken);
        var response = new { status = health.DatabaseUsable ? "Healthy" : "Unavailable",
            api = "Healthy", database = health.DatabaseUsable ? "Healthy" : "Unavailable" };
        return health.DatabaseUsable ? Ok(response) : StatusCode(503, response);
    }
}

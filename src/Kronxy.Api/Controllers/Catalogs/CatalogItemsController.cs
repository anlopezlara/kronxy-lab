using Kronxy.Application.CatalogItems.ActivateCatalogItem;
using Kronxy.Application.CatalogItems.DeactivateCatalogItem;
using Kronxy.Application.CatalogItems.UpdateCatalogItem;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Kronxy.Api.Controllers.Catalogs;

[ApiController]
[Route("api/catalog-items")]
public sealed class CatalogItemsController : ControllerBase
{
    private readonly ISender _sender;

    public CatalogItemsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCatalogItem(
        Guid id,
        UpdateCatalogItemRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCatalogItemCommand(
            id,
            request.Code,
            request.Name,
            request.Description,
            request.Value,
            request.SortOrder);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeactivateCatalogItem(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeactivateCatalogItemCommand(id);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpPut("{id}/activate")]
    public async Task<IActionResult> ActivateCatalogItem(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new ActivateCatalogItemCommand(id);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }
}
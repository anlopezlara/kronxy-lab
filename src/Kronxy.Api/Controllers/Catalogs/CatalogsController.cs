using Kronxy.Application.CatalogItems.CreateCatalogItem;
using Kronxy.Application.CatalogItems.GetCatalogItem;
using Kronxy.Application.Catalogs.ActivateCatalog;
using Kronxy.Application.Catalogs.CreateCatalog;
using Kronxy.Application.Catalogs.DeactivateCatalog;
using Kronxy.Application.Catalogs.GetCatalog;
using Kronxy.Application.Catalogs.UpdateCatalog;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Kronxy.Api.Controllers.Catalogs;

[ApiController]
[Route("api/catalogs")]
public sealed class CatalogsController : ControllerBase
{
    private readonly ISender _sender;

    public CatalogsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> CreateCatalog(
        CreateCatalogRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateCatalogCommand(
            request.Code,
            request.Name,
            request.Description,
            request.IsSystem);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return CreatedAtAction(
            nameof(GetCatalog),
            new { id = result.Value },
            result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetCatalogs(
        CancellationToken cancellationToken)
    {
        var query = new GetCatalogsQuery();

        var result = await _sender.Send(query, cancellationToken);

        return Ok(result.Value);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCatalog(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetCatalogQuery(id);

        var result = await _sender.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCatalog(
        Guid id,
        UpdateCatalogRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCatalogCommand(
            id,
            request.Code,
            request.Name,
            request.Description);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeactivateCatalog(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeactivateCatalogCommand(id);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpPut("{id}/activate")]
    public async Task<IActionResult> ActivateCatalog(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new ActivateCatalogCommand(id);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpPost("{catalogId}/items")]
    public async Task<IActionResult> CreateCatalogItem(
        Guid catalogId,
        CreateCatalogItemRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateCatalogItemCommand(
            catalogId,
            request.Code,
            request.Name,
            request.Description,
            request.Value,
            request.SortOrder,
            request.IsSystem);

        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return CreatedAtAction(
            nameof(GetCatalogItem),
            new { itemId = result.Value },
            result.Value);
    }

    [HttpGet("{catalogId}/items")]
    public async Task<IActionResult> GetCatalogItems(
        Guid catalogId,
        CancellationToken cancellationToken)
    {
        var query = new GetCatalogItemsQuery(catalogId);

        var result = await _sender.Send(query, cancellationToken);

        return Ok(result.Value);
    }

    [HttpGet("items/{itemId}")]
    public async Task<IActionResult> GetCatalogItem(
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var query = new GetCatalogItemQuery(itemId);

        var result = await _sender.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
    }

    [HttpGet("by-code/{code}")]
    public async Task<IActionResult> GetCatalogByCode(
    string code,
    CancellationToken cancellationToken)
    {
        var query = new GetCatalogByCodeQuery(code);

        var result = await _sender.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
    }

    [HttpGet("by-code/{code}/items")]
    public async Task<IActionResult> GetCatalogItemsByCatalogCode(
    string code,
    CancellationToken cancellationToken)
    {
        var query = new GetCatalogItemsByCatalogCodeQuery(code);

        var result = await _sender.Send(query, cancellationToken);

        return Ok(result.Value);
    }

}
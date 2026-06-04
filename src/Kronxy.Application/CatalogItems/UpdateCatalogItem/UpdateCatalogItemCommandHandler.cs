using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Catalogs;

namespace Kronxy.Application.CatalogItems.UpdateCatalogItem;

internal sealed class UpdateCatalogItemCommandHandler
    : ICommandHandler<UpdateCatalogItemCommand>
{
    private readonly ICatalogRepository _catalogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdateCatalogItemCommandHandler(
        ICatalogRepository catalogRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _catalogRepository = catalogRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> Handle(
        UpdateCatalogItemCommand request,
        CancellationToken cancellationToken)
    {
        CatalogItem? item = await _catalogRepository.GetItemByIdAsync(
            request.CatalogItemId,
            cancellationToken);

        if (item is null)
        {
            return Result.Failure(CatalogErrors.ItemNotFound);
        }

        bool codeExists = await _catalogRepository.ExistsItemCodeAsync(
            item.CatalogId,
            request.Code,
            cancellationToken);

        if (codeExists &&
            item.Code != request.Code.Trim().ToUpperInvariant())
        {
            return Result.Failure(CatalogErrors.ItemCodeNotUnique);
        }

        item.Update(
            request.Code,
            request.Name,
            request.Description,
            request.Value,
            request.SortOrder,
            _dateTimeProvider.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Catalogs;

namespace Kronxy.Application.CatalogItems.CreateCatalogItem;

internal sealed class CreateCatalogItemCommandHandler
    : ICommandHandler<CreateCatalogItemCommand, Guid>
{
    private readonly ICatalogRepository _catalogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateCatalogItemCommandHandler(
        ICatalogRepository catalogRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _catalogRepository = catalogRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Guid>> Handle(
        CreateCatalogItemCommand request,
        CancellationToken cancellationToken)
    {
        Catalog? catalog = await _catalogRepository.GetByIdAsync(
            request.CatalogId,
            cancellationToken);

        if (catalog is null)
        {
            return Result.Failure<Guid>(CatalogErrors.NotFound);
        }

        bool itemCodeExists = await _catalogRepository.ExistsItemCodeAsync(
            request.CatalogId,
            request.Code,
            cancellationToken);

        if (itemCodeExists)
        {
            return Result.Failure<Guid>(CatalogErrors.ItemCodeNotUnique);
        }

        CatalogItem item = CatalogItem.Create(
            request.CatalogId,
            request.Code,
            request.Name,
            request.Description,
            request.Value,
            request.SortOrder,
            request.IsSystem,
            _dateTimeProvider.UtcNow);

        _catalogRepository.AddItem(item);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}
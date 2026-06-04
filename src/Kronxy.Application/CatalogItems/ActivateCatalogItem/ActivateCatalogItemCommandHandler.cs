using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Catalogs;

namespace Kronxy.Application.CatalogItems.ActivateCatalogItem;

internal sealed class ActivateCatalogItemCommandHandler
    : ICommandHandler<ActivateCatalogItemCommand>
{
    private readonly ICatalogRepository _catalogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ActivateCatalogItemCommandHandler(
        ICatalogRepository catalogRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _catalogRepository = catalogRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> Handle(
        ActivateCatalogItemCommand request,
        CancellationToken cancellationToken)
    {
        CatalogItem? item = await _catalogRepository.GetItemByIdAsync(
            request.CatalogItemId,
            cancellationToken);

        if (item is null)
        {
            return Result.Failure(CatalogErrors.ItemNotFound);
        }

        item.Activate(_dateTimeProvider.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
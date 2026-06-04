using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Catalogs;

namespace Kronxy.Application.Catalogs.ActivateCatalog;

internal sealed class ActivateCatalogCommandHandler
    : ICommandHandler<ActivateCatalogCommand>
{
    private readonly ICatalogRepository _catalogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ActivateCatalogCommandHandler(
        ICatalogRepository catalogRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _catalogRepository = catalogRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> Handle(
        ActivateCatalogCommand request,
        CancellationToken cancellationToken)
    {
        Catalog? catalog = await _catalogRepository.GetByIdAsync(
            request.CatalogId,
            cancellationToken);

        if (catalog is null)
        {
            return Result.Failure(CatalogErrors.NotFound);
        }

        catalog.Activate(_dateTimeProvider.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
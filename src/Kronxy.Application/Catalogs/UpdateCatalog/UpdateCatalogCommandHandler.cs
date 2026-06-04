using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Catalogs;

namespace Kronxy.Application.Catalogs.UpdateCatalog;

internal sealed class UpdateCatalogCommandHandler
    : ICommandHandler<UpdateCatalogCommand>
{
    private readonly ICatalogRepository _catalogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdateCatalogCommandHandler(
        ICatalogRepository catalogRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _catalogRepository = catalogRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> Handle(
        UpdateCatalogCommand request,
        CancellationToken cancellationToken)
    {
        Catalog? catalog = await _catalogRepository.GetByIdAsync(
            request.CatalogId,
            cancellationToken);

        if (catalog is null)
        {
            return Result.Failure(CatalogErrors.NotFound);
        }

        string normalizedCode = request.Code.Trim().ToUpperInvariant();

        if (catalog.Code != normalizedCode)
        {
            bool codeExists = await _catalogRepository.ExistsByCodeAsync(
                request.Code,
                cancellationToken);

            if (codeExists)
            {
                return Result.Failure(CatalogErrors.CodeNotUnique);
            }
        }

        catalog.Update(
            request.Code,
            request.Name,
            request.Description,
            _dateTimeProvider.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
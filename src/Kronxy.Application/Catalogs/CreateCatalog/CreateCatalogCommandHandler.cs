using Kronxy.Application.Abstractions.Clock;
using Kronxy.Application.Abstractions.Messaging;
using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Catalogs;

namespace Kronxy.Application.Catalogs.CreateCatalog;

internal sealed class CreateCatalogCommandHandler
    : ICommandHandler<CreateCatalogCommand, Guid>
{
    private readonly ICatalogRepository _catalogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateCatalogCommandHandler(
        ICatalogRepository catalogRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _catalogRepository = catalogRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Guid>> Handle(
        CreateCatalogCommand request,
        CancellationToken cancellationToken)
    {
        bool codeExists = await _catalogRepository.ExistsByCodeAsync(
            request.Code,
            cancellationToken);

        if (codeExists)
        {
            return Result.Failure<Guid>(CatalogErrors.CodeNotUnique);
        }

        var catalog = Catalog.Create(
            request.Code,
            request.Name,
            request.Description,
            request.IsSystem,
            _dateTimeProvider.UtcNow);

        _catalogRepository.Add(catalog);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return catalog.Id;
    }
}
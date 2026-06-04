namespace Kronxy.Domain.Catalogs;

public interface ICatalogRepository
{
    Task<Catalog?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Catalog?> GetByIdWithItemsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Catalog?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsItemCodeAsync(
        Guid catalogId,
        string code,
        CancellationToken cancellationToken = default);

    Task<CatalogItem?> GetItemByIdAsync(
        Guid itemId,
        CancellationToken cancellationToken = default);

    Task<Catalog?> GetByCodeWithItemsAsync(
    string code,
    CancellationToken cancellationToken = default);

    Task<bool> IsActiveItemInCatalogAsync(
    Guid itemId,
    string catalogCode,
    CancellationToken cancellationToken = default);

    void Add(Catalog catalog);

    void AddItem(CatalogItem item);

    void Remove(Catalog catalog);

    void RemoveItem(CatalogItem item);
}
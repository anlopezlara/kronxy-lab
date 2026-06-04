using Kronxy.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;

namespace Kronxy.Infrastructure.Repositories;

internal sealed class CatalogRepository : Repository<Catalog>, ICatalogRepository
{
    public CatalogRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public Task<Catalog?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return DbContext
            .Set<Catalog>()
            .FirstOrDefaultAsync(catalog => catalog.Id == id, cancellationToken);
    }

    public Task<Catalog?> GetByIdWithItemsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return DbContext
            .Set<Catalog>()
            .Include(catalog => catalog.Items)
            .FirstOrDefaultAsync(catalog => catalog.Id == id, cancellationToken);
    }

    public Task<Catalog?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        string normalizedCode = code.Trim().ToUpperInvariant();

        return DbContext
            .Set<Catalog>()
            .FirstOrDefaultAsync(catalog => catalog.Code == normalizedCode, cancellationToken);
    }

    public Task<bool> ExistsByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        string normalizedCode = code.Trim().ToUpperInvariant();

        return DbContext
            .Set<Catalog>()
            .AnyAsync(catalog => catalog.Code == normalizedCode, cancellationToken);
    }

    public Task<bool> ExistsItemCodeAsync(
        Guid catalogId,
        string code,
        CancellationToken cancellationToken = default)
    {
        string normalizedCode = code.Trim().ToUpperInvariant();

        return DbContext
            .Set<CatalogItem>()
            .AnyAsync(
                item => item.CatalogId == catalogId &&
                        item.Code == normalizedCode,
                cancellationToken);
    }

    public Task<CatalogItem?> GetItemByIdAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        return DbContext
            .Set<CatalogItem>()
            .FirstOrDefaultAsync(item => item.Id == itemId, cancellationToken);
    }

    public Task<Catalog?> GetByCodeWithItemsAsync(
    string code,
    CancellationToken cancellationToken = default)
    {
        string normalizedCode = code.Trim().ToUpperInvariant();

        return DbContext
            .Set<Catalog>()
            .Include(catalog => catalog.Items)
            .FirstOrDefaultAsync(catalog => catalog.Code == normalizedCode, cancellationToken);
    }

    public void Add(Catalog catalog)
    {
        DbContext.Set<Catalog>().Add(catalog);
    }

    public void AddItem(CatalogItem item)
    {
        DbContext.Set<CatalogItem>().Add(item);
    }

    public void Remove(Catalog catalog)
    {
        DbContext.Set<Catalog>().Remove(catalog);
    }

    public void RemoveItem(CatalogItem item)
    {
        DbContext.Set<CatalogItem>().Remove(item);
    }

    public Task<bool> IsActiveItemInCatalogAsync(
    Guid itemId,
    string catalogCode,
    CancellationToken cancellationToken = default)
    {
        string normalizedCatalogCode = catalogCode.Trim().ToUpperInvariant();

        return DbContext
            .Set<CatalogItem>()
            .Join(
                DbContext.Set<Catalog>(),
                item => item.CatalogId,
                catalog => catalog.Id,
                (item, catalog) => new { item, catalog })
            .AnyAsync(
                x => x.item.Id == itemId &&
                     x.item.IsActive &&
                     x.catalog.Code == normalizedCatalogCode &&
                     x.catalog.IsActive,
                cancellationToken);
    }

}
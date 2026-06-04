using Kronxy.Domain.Abstractions;

namespace Kronxy.Domain.Catalogs;

public static class CatalogErrors
{
    public static readonly Error NotFound = new(
        "Catalog.NotFound",
        "The catalog with the specified identifier was not found");

    public static readonly Error CodeNotUnique = new(
        "Catalog.CodeNotUnique",
        "The specified catalog code is already in use");

    public static readonly Error CannotDeleteSystemCatalog = new(
        "Catalog.CannotDeleteSystemCatalog",
        "System catalogs cannot be deleted");

    public static readonly Error ItemNotFound = new(
        "CatalogItem.NotFound",
        "The catalog item with the specified identifier was not found");

    public static readonly Error ItemCodeNotUnique = new(
        "CatalogItem.CodeNotUnique",
        "The specified catalog item code is already in use for this catalog");

    public static readonly Error CannotDeleteSystemItem = new(
        "CatalogItem.CannotDeleteSystemItem",
        "System catalog items cannot be deleted");
}
using Kronxy.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kronxy.Infrastructure.Configurations;

internal sealed class CatalogItemConfiguration : IEntityTypeConfiguration<CatalogItem>
{
    public void Configure(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.ToTable("catalog_items");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id)
            .HasColumnName("id");

        builder.Property(item => item.CatalogId)
            .HasColumnName("catalog_id");

        builder.Property(item => item.Code)
            .HasMaxLength(50)
            .HasColumnName("code");

        builder.Property(item => item.Name)
            .HasMaxLength(100)
            .HasColumnName("name");

        builder.Property(item => item.Description)
            .HasMaxLength(300)
            .HasColumnName("description");

        builder.Property(item => item.Value)
            .HasMaxLength(500)
            .HasColumnName("value");

        builder.Property(item => item.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(item => item.IsSystem)
            .HasColumnName("is_system");

        builder.Property(item => item.IsActive)
            .HasColumnName("is_active");

        builder.Property(item => item.CreatedOnUtc)
            .HasColumnName("created_on_utc");

        builder.Property(item => item.UpdatedOnUtc)
            .HasColumnName("updated_on_utc");

        builder.Property(item => item.DeletedOnUtc)
            .HasColumnName("deleted_on_utc");

        builder.HasIndex(item => item.CatalogId);

        builder.HasIndex(item => new { item.CatalogId, item.Code })
            .IsUnique();
    }
}
using Kronxy.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kronxy.Infrastructure.Configurations;

internal sealed class CatalogConfiguration : IEntityTypeConfiguration<Catalog>
{
    public void Configure(EntityTypeBuilder<Catalog> builder)
    {
        builder.ToTable("catalogs");

        builder.HasKey(catalog => catalog.Id);

        builder.Property(catalog => catalog.Id)
            .HasColumnName("id");

        builder.Property(catalog => catalog.Code)
            .HasMaxLength(50)
            .HasColumnName("code");

        builder.Property(catalog => catalog.Name)
            .HasMaxLength(100)
            .HasColumnName("name");

        builder.Property(catalog => catalog.Description)
            .HasMaxLength(300)
            .HasColumnName("description");

        builder.Property(catalog => catalog.IsSystem)
            .HasColumnName("is_system");

        builder.Property(catalog => catalog.IsActive)
            .HasColumnName("is_active");

        builder.Property(catalog => catalog.CreatedOnUtc)
            .HasColumnName("created_on_utc");

        builder.Property(catalog => catalog.UpdatedOnUtc)
            .HasColumnName("updated_on_utc");

        builder.Property(catalog => catalog.DeletedOnUtc)
            .HasColumnName("deleted_on_utc");

        builder.HasIndex(catalog => catalog.Code)
            .IsUnique();

        builder.HasMany(catalog => catalog.Items)
            .WithOne()
            .HasForeignKey(item => item.CatalogId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_catalog_items_catalogs_catalog_id");

        builder.Metadata
            .FindNavigation(nameof(Catalog.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
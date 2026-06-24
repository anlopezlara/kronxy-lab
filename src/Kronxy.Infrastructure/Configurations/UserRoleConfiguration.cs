using Kronxy.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Kronxy.Infrastructure.Configurations;
internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");
        builder.HasKey(userRole => userRole.Id);
        builder.Property(userRole => userRole.Id).HasColumnName("id");
        builder.Property(userRole => userRole.Code).HasMaxLength(50).HasColumnName("code");
        builder.Property(userRole => userRole.Name).HasMaxLength(200).HasColumnName("name");
        builder.Property(userRole => userRole.Description).HasMaxLength(1000).HasColumnName("description");
        builder.Property(userRole => userRole.DisplayOrder).HasColumnName("display_order");
        builder.Property(userRole => userRole.IsActive).HasColumnName("is_active");
        builder.Property(userRole => userRole.CreatedOnUtc).HasColumnName("created_on_utc");
        builder.Property(userRole => userRole.UpdatedOnUtc).HasColumnName("updated_on_utc");
        builder.Property(userRole => userRole.DeletedOnUtc).HasColumnName("deleted_on_utc");
        builder.HasIndex(userRole => userRole.Code).IsUnique();
        builder.HasIndex(userRole => userRole.DisplayOrder);
    }
}

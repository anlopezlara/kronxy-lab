using Kronxy.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kronxy.Infrastructure.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Username)
            .HasMaxLength(100)
            .HasConversion(
                username => username.Value,
                value => new Username(value));

        builder.HasIndex(user => user.Username).IsUnique();

        builder.Property(user => user.FirstName)
            .HasMaxLength(200)
            .HasConversion(firstName => firstName.Value, value => new FirstName(value));

        builder.Property(user => user.LastName)
            .HasMaxLength(200)
            .HasConversion(firstName => firstName.Value, value => new LastName(value));

        builder.Property(user => user.Email)
            .HasMaxLength(400)
            .HasConversion(email => email.Value, value => new Domain.Users.Email(value)); ;

        builder.HasIndex(user => user.Email).IsUnique();

        builder.Property(user => user.PhoneNumber)
            .HasMaxLength(30)
            .HasConversion(
                phone => phone == null ? null : phone.Value,
                value => value == null ? null : new PhoneNumber(value)
            );

        builder.Property(user => user.Username)
            .HasMaxLength(100)
            .HasConversion(
                username => username.Value,
                value => new Username(value)
            );

        builder.HasIndex(user => user.Username).IsUnique();

        builder.Property(user => user.RoleId)
            .HasColumnName("role_id");

        builder.HasOne<Kronxy.Domain.Catalogs.CatalogItem>()
            .WithMany()
            .HasForeignKey(user => user.RoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_users_catalog_items_role_id");

        builder.Property(user => user.RoleId)
            .IsRequired()
            .HasColumnName("role_id");

    }
}

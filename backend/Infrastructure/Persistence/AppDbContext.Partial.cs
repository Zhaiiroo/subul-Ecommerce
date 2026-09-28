using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using AttributeEntity = backend.Domain.Entities.Attribute;

namespace backend.Infrastructure.Persistence;

public partial class AppDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<Product>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<Brand>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<Collection>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<CollectionProduct>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<AttributeGroup>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<AttributeEntity>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<ShippingZone>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<ShippingRate>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<Address>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<Cart>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<CartItem>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<Order>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<OrderItem>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<OrderStatusHistory>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<User>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<PaymentMethod>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<AdminUser>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();

        // Added after the scaffold, so the mapping lives here rather than in the
        // generated AppDbContext.cs. The matching DDL is in
        // docs/sql/2026-09-10-admin-user-password-policy.sql.
        modelBuilder.Entity<AdminUser>()
            .Property(e => e.MustChangePassword)
            .HasDefaultValue(false)
            .HasColumnName("must_change_password");

        modelBuilder.Entity<AdminUser>()
            .Property(e => e.PasswordChangedAt)
            .HasColumnType("timestamp without time zone")
            .HasColumnName("password_changed_at");
    }
}

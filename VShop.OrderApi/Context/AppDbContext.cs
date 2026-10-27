using Microsoft.EntityFrameworkCore;
using VShop.OrderApi.Models;

namespace VShop.OrderApi.Context;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<OrderHeader> OrderHeaders => Set<OrderHeader>();
    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();
    public DbSet<CheckoutSaga> CheckoutSagas => Set<CheckoutSaga>();
    public DbSet<CheckoutSagaItem> CheckoutSagaItems => Set<CheckoutSagaItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OrderHeader>(entity =>
        {
            entity.HasKey(x => x.OrderHeaderId);
            entity.Property(x => x.UserId).HasMaxLength(450).UseCollation("Latin1_General_100_BIN2").IsRequired();
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Telephone).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Address).HasMaxLength(300);
            entity.Property(x => x.CouponCode).HasMaxLength(50);
            entity.Property(x => x.PaymentMethod).HasMaxLength(50).IsRequired();
            entity.Property(x => x.IdempotencyKey).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2").IsRequired();
            entity.Property(x => x.SubtotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(30);
        });

        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.HasKey(x => x.OrderDetailId);
            entity.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ProductDescription).HasMaxLength(1000);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);
            entity.HasOne(x => x.OrderHeader)
                .WithMany(x => x.OrderDetails)
                .HasForeignKey(x => x.OrderHeaderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CheckoutSaga>(entity =>
        {
            entity.HasKey(x => x.OrderHeaderId);
            entity.Property(x => x.Phase).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.LastError).HasMaxLength(500);
            entity.HasOne(x => x.Order).WithOne().HasForeignKey<CheckoutSaga>(x => x.OrderHeaderId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CheckoutSagaItem>(entity =>
        {
            entity.HasKey(x => x.OrderDetailId);
            entity.HasIndex(x => x.ReservationId).IsUnique();
            entity.HasOne(x => x.OrderDetail).WithOne().HasForeignKey<CheckoutSagaItem>(x => x.OrderDetailId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Saga).WithMany(x => x.Items).HasForeignKey(x => x.OrderHeaderId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

using Microsoft.EntityFrameworkCore;
using VShop.DiscountApi.Models;

namespace VShop.DiscountApi.Context;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponReservation> CouponReservations => Set<CouponReservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Coupon>(entity =>
        {
            entity.HasKey(c => c.CouponId);
            entity.Property(c => c.CouponCode).IsRequired().HasMaxLength(50);
            entity.HasIndex(c => c.CouponCode).IsUnique();
            entity.Property(c => c.DiscountAmount).HasPrecision(18, 2);
            entity.Property(c => c.IsActive).IsRequired();

            entity.HasData(
                new Coupon { CouponId = 1, CouponCode = "VSHOP10", DiscountAmount = 10m, IsActive = true },
                new Coupon { CouponId = 2, CouponCode = "VSHOP20", DiscountAmount = 20m, IsActive = true });
        });
        modelBuilder.Entity<CouponReservation>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.UserId).IsRequired().HasMaxLength(450);
            entity.Property(r => r.Status).HasConversion<int>();
            entity.Property(r => r.DiscountAmount).HasPrecision(18, 2);
            entity.HasIndex(r => new { r.CouponId, r.UserId, r.Status });
            entity.HasOne<Coupon>().WithMany().HasForeignKey(r => r.CouponId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

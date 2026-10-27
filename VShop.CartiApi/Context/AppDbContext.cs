
using Microsoft.EntityFrameworkCore;
using VShop.CartiApi.Models;

namespace VShop.CartiApi.Context
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<CartHeader> CartHeaders { get; set; } = null!;

        public DbSet<CartItem> CartItems { get; set; } = null!;

        public DbSet<Product> Products { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =========================
            // PRODUCT
            // =========================

            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(p => p.ProductId);

                entity.Property(p => p.ProductId)
                    .ValueGeneratedNever();

                entity.Property(p => p.Name)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(p => p.Description)
                    .HasMaxLength(255);

                entity.Property(p => p.ImageUrl)
                    .HasMaxLength(255);

                entity.Property(p => p.CategoryName)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(p => p.Price)
                    .HasPrecision(18, 2);

                entity.Property(p => p.Stock)
                    .IsRequired();
            });

            // =========================
            // CART HEADER
            // =========================

            modelBuilder.Entity<CartHeader>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.Property(c => c.UserId)
                    .HasMaxLength(450)
                    .IsRequired();

                entity.Property(c => c.CouponCode)
                    .HasMaxLength(50);

                entity.HasIndex(c => c.UserId);
            });

            // =========================
            // CART ITEM
            // =========================

            modelBuilder.Entity<CartItem>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.Property(c => c.Quantity)
                    .HasDefaultValue(1)
                    .IsRequired();

                entity.HasOne(c => c.CartHeader)
                    .WithMany()
                    .HasForeignKey(c => c.CartHeaderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.Product)
                    .WithMany()
                    .HasForeignKey(c => c.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(c => new
                {
                    c.CartHeaderId,
                    c.ProductId
                }).IsUnique();
            });
        }
    }
}

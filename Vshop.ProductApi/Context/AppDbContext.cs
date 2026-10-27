using Microsoft.EntityFrameworkCore;
using Vshop.ProductApi.Models;

namespace Vshop.ProductApi.Context
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {

        }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<StockReservation> StockReservations => Set<StockReservation>();

        // Fluent API
        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<Category>().HasKey(c => c.CategoryId);
            mb.Entity<Category>().Property(c => c.Name).IsRequired().HasMaxLength(100);
            mb.Entity<Category>().Property(c => c.Description).HasMaxLength(500);
            mb.Entity<Product>().HasKey(p => p.ProductId);
            mb.Entity<Product>().Property(p => p.Name).IsRequired().HasMaxLength(100);
            mb.Entity<Product>().Property(p => p.Description).HasMaxLength(500);
            mb.Entity<Product>().Property(p => p.Price).IsRequired().HasColumnType("decimal(18,2)");
            mb.Entity<Product>().Property(p => p.Stock).IsRequired();
            mb.Entity<Product>().Property(p => p.ImageUrl).HasMaxLength(200).IsRequired();
            mb.Entity<StockReservation>(e =>
            {
                e.HasKey(r => r.Id);
                e.Property(r => r.UserId).IsRequired().HasMaxLength(450);
                e.Property(r => r.Status).HasConversion<int>();
                e.HasIndex(r => new { r.ProductId, r.Status, r.ExpiresAt });
                e.HasOne<Product>().WithMany().HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.Restrict);
            });
            // Relationship
            mb.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            mb.Entity<Category>().HasData(
                        new Category
                        {
                            CategoryId = 1, Name = "Material Escolar", Description = "Material escolar para uso em aula"
                        },
                        new Category
                        {
                            CategoryId = 2,
                            Name = "Acessórios",
                            Description = "Acessórios para uso pessoal"
                        }
    


                    );


        }
    }
}

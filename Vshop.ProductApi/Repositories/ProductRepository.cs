using Microsoft.EntityFrameworkCore;
using Vshop.ProductApi.Context;
using Vshop.ProductApi.Models;

namespace Vshop.ProductApi.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly AppDbContext _context;

        public ProductRepository(AppDbContext context)
        {
            _context = context;
        }

        // Consultas
        public async Task<IEnumerable<Product>> GetAll()
        {
            return await _context.Products
                .Include(p => p.Category)
                .ToListAsync();
        }

        public async Task<Product?> GetProductById(int id)
        {
            return await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductId == id);
        }

        // Criar
        public async Task<Product> CreateProduct(Product product)
        {
            _context.Products.Add(product);

            await _context.SaveChangesAsync();

            return product;
        }

        // Atualizar
        public async Task<Product> UpdateProduct(Product product)
        {
            _context.Entry(product).State = EntityState.Modified;

            await _context.SaveChangesAsync();

            return product;
        }

        // Deletar
        public async Task<Product> DeleteProduct(int id)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
            {
                throw new KeyNotFoundException(
                    $"Product {id} not found.");
            }

            _context.Products.Remove(product);

            await _context.SaveChangesAsync();

            return product;
        }
    }
}
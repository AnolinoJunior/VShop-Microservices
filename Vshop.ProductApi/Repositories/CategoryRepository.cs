using Microsoft.EntityFrameworkCore;
using Vshop.ProductApi.Context;
using Vshop.ProductApi.Models;

namespace Vshop.ProductApi.Repositories
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly AppDbContext _context;

        public CategoryRepository(AppDbContext context)
        {
            _context = context;
        }

        // Consultas
        public async Task<IEnumerable<Category>> GetAll()
        {
            return await _context.Categories.ToListAsync();
        }

        public async Task<IEnumerable<Category>> GetCategoriesProducts()
        {
            return await _context.Categories
                .Include(c => c.Products)
                .ToListAsync();
        }

        public async Task<Category?> GetCategoryById(int id)
        {
            return await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == id);
        }

        // Criar
        public async Task<Category> CreateCategory(Category category)
        {
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return category;
        }

        // Atualizar
        public async Task<Category> UpdateCategory(Category category)
        {
            _context.Entry(category).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return category;
        }

        // Deletar
        public async Task<Category> DeleteCategory(int id)
        {
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == id);

            if (category == null)
            {
                throw new KeyNotFoundException(
                    $"Category {id} not found.");
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();


            return category;
        }
    }
}   
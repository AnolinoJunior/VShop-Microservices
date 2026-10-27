using Vshop.ProductApi.Models;

namespace Vshop.ProductApi.Repositories
{
    public interface ICategoryRepository
    {
        Task<IEnumerable<Category>> GetAll();

        Task<IEnumerable<Category>> GetCategoriesProducts();

        Task<Category?> GetCategoryById(int id);

        Task<Category> CreateCategory(Category category);

        Task<Category> UpdateCategory(Category category);

        Task<Category> DeleteCategory(int id);
    }
}
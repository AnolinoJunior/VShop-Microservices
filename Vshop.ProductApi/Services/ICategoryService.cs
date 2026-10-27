using Vshop.ProductApi.DTOs;

namespace Vshop.ProductApi.Services
{
    public interface ICategoryService
    {
        Task<IEnumerable<CategoryDTO>> GetAllCategories();

        Task <IEnumerable<CategoryDTO>> GetCategoriesProducts();

        Task <CategoryDTO> GetCategoryById(int id);

        Task AddCategory(CategoryDTO category);
        
        Task UpdateCategory(int id, CategoryDTO category);
        
        Task RemoveCategory(int id);
    }
}

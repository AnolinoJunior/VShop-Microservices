using AutoMapper;
using Vshop.ProductApi.DTOs;
using Vshop.ProductApi.Models;
using Vshop.ProductApi.Repositories;

namespace Vshop.ProductApi.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;

        public CategoryService(
            ICategoryRepository categoryRepository,
            IMapper mapper)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<CategoryDTO>> GetAllCategories()
        {
            var categoriesEntity = await _categoryRepository.GetAll();

            return _mapper.Map<IEnumerable<CategoryDTO>>(categoriesEntity);
        }

        public async Task<IEnumerable<CategoryDTO>> GetCategoriesProducts()
        {
            var categoriesEntity =
                await _categoryRepository.GetCategoriesProducts();

            return _mapper.Map<IEnumerable<CategoryDTO>>(categoriesEntity);
        }

        public async Task<CategoryDTO> GetCategoryById(int id)
        {
            var categoryEntity =
                await _categoryRepository.GetCategoryById(id);

            return _mapper.Map<CategoryDTO>(categoryEntity);
        }

        public async Task AddCategory(CategoryDTO categoryDto)
        {
            var categoryEntity =
                _mapper.Map<Category>(categoryDto);

            await _categoryRepository.CreateCategory(categoryEntity);

            categoryDto.CategoryId = categoryEntity.CategoryId;
        }

        public async Task UpdateCategory(int id, CategoryDTO categoryDto)
        {
            var categoryEntity =
                _mapper.Map<Category>(categoryDto);

            categoryEntity.CategoryId = id;

            await _categoryRepository.UpdateCategory(categoryEntity);
        }

        public async Task RemoveCategory(int id)
        {
            var categoryEntity =
                await _categoryRepository.GetCategoryById(id);

            await _categoryRepository.DeleteCategory(categoryEntity!.CategoryId);
        }
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vshop.ProductApi.DTOs;
using Vshop.ProductApi.Roles;
using Vshop.ProductApi.Services;

namespace Vshop.ProductApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public CategoriesController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        // GET: api/categories
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CategoryDTO>>> Get()
        {
            var categories = await _categoryService.GetAllCategories();

            return Ok(categories);
        }

        // GET: api/categories/products
        [HttpGet("products")]
        public async Task<ActionResult<IEnumerable<CategoryDTO>>> GetCategoriesProducts()
        {
            var categories = await _categoryService.GetCategoriesProducts();

            return Ok(categories);
        }

        // GET: api/categories/1
        [HttpGet("{id:int}")]
        public async Task<ActionResult<CategoryDTO>> GetById(int id)
        {
            var category = await _categoryService.GetCategoryById(id);

            if (category == null)
                return NotFound();

            return Ok(category);
        }

        // POST: api/categories
        [HttpPost]
        public async Task<ActionResult> Post(CategoryDTO categoryDto)
        {
            await _categoryService.AddCategory(categoryDto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = categoryDto.CategoryId },
                categoryDto);
        }

        // PUT: api/categories/1
        [HttpPut("{id:int}")]
        public async Task<ActionResult> Put(int id, CategoryDTO categoryDto)
        {
            if (id != categoryDto.CategoryId)
                return BadRequest();

            await _categoryService.UpdateCategory(id, categoryDto);

            return NoContent();
        }

        // DELETE: api/categories/1
        [HttpDelete("{id:int}")]
        [Authorize(Roles = Role.Admin)]
        public async Task<ActionResult> Delete(int id)
        {
            await _categoryService.RemoveCategory(id);

            return NoContent();
        }
    }
}
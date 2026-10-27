using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vshop.ProductApi.DTOs;
using Vshop.ProductApi.Roles;
using Vshop.ProductApi.Services;

namespace Vshop.ProductApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
   
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        // GET: api/products
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductDTO>>> Get()
        {
            var products = await _productService.GetAllProducts();

            return Ok(products);
        }

        // GET: api/products/1
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ProductDTO>> GetById(int id)
        {
            var product = await _productService.GetProductById(id);

            if (product == null)
                return NotFound();

            return Ok(product);
        }

        // POST: api/products
        [HttpPost]
        [Authorize(Roles = Role.Admin)]
        public async Task<ActionResult> Post(ProductDTO productDto)
        {
            await _productService.AddProduct(productDto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = productDto.ProductId },
                productDto);
        }

        // PUT: api/products/1
        [HttpPut("{id:int}")]
        [Authorize(Roles = Role.Admin)]
        public async Task<ActionResult> Put(int id, ProductDTO productDto)
        {
            if (id != productDto.ProductId)
                return BadRequest();

            await _productService.UpdateProduct(id, productDto);

            return NoContent();
        }

        // DELETE: api/products/1
        [HttpDelete("{id:int}")]
        [Authorize(Roles = Role.Admin)]
        public async Task<ActionResult> Delete(int id)
        {
            await _productService.RemoveProduct(id);

            return NoContent();
        }
    }
}
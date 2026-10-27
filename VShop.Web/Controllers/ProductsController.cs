
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Vshop.Web.Models;
using Vshop.Web.Services.Contracts;
using VShop.Web.Roles;

namespace Vshop.Web.Controllers
{
    [Authorize(Roles = Role.Admin)]
    public class ProductsController : Controller
    {
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProductsController(
            IProductService productService,
            ICategoryService categoryService,
            IWebHostEnvironment webHostEnvironment)
        {
            _productService = productService;
            _categoryService = categoryService;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Products
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var products =
                await _productService.GetAllProducts(
                    await GetAccessToken());

            if (products == null)
                return View("Error");

            return View(products);
        }

        // GET: Products/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var product =
                await _productService.FindProductById(
                    id,
                    await GetAccessToken());

            if (product == null)
                return NotFound();

            return View(product);
        }

        // GET: Products/CreateProduct
        
        [HttpGet]
        public async Task<IActionResult> CreateProduct()
        {
            var categories =
                await _categoryService.GetAllCategories(
                    await GetAccessToken());

            if (categories == null)
                return View("Error");

            ViewBag.CategoryId = new SelectList(
                categories,
                "CategoryId",
                "Name");

            return View();
        }

        // POST: Products/CreateProduct
      
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(
            ProductViewModel productVM)
        {
            if (ModelState.IsValid)
            {
                var product =
                    await _productService.CreateProduct(
                        productVM,
                        await GetAccessToken());

                if (product != null)
                    return RedirectToAction(nameof(Index));
            }

            var categories =
                await _categoryService.GetAllCategories(
                    await GetAccessToken());

            if (categories == null)
                return View("Error");

            ViewBag.CategoryId = new SelectList(
                categories,
                "CategoryId",
                "Name");

            return View(productVM);
        }

        // GET: Products/UpdateProduct/5
  
        [HttpGet]
        public async Task<IActionResult> UpdateProduct(int id)
        {
            var token = await GetAccessToken();

            var categories =
                await _categoryService.GetAllCategories(token);

            if (categories == null)
                return View("Error");

            ViewBag.CategoryId = new SelectList(
                categories,
                "CategoryId",
                "Name");

            var product =
                await _productService.FindProductById(
                    id,
                    token);

            if (product == null)
                return View("Error");

            return View(product);
        }

        // POST: Products/UpdateProduct
     
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProduct(
            ProductViewModel productVM)
        {
            var token = await GetAccessToken();

            if (!ModelState.IsValid)
            {
                var categories =
                    await _categoryService.GetAllCategories(token);

                if (categories == null)
                    return View("Error");

                ViewBag.CategoryId = new SelectList(
                    categories,
                    "CategoryId",
                    "Name");

                return View(productVM);
            }

            var product =
                await _productService.UpdateProduct(
                    productVM,
                    token);

            if (product == null)
                return View("Error");

            return RedirectToAction(nameof(Index));
        }

     
        [HttpGet]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product =
                await _productService.FindProductById(
                    id,
                    await GetAccessToken());

            if (product == null)
                return View("Error");

            return View(product);
        }

        // POST: Products/DeleteProduct/5
   
        [HttpPost]
        [ActionName("DeleteProduct")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var deleted =
                await _productService.DeleteProductById(
                    id,
                    await GetAccessToken());

            if (!deleted)
                return View("Error");

            return RedirectToAction(nameof(Index));
        }

        private async Task<string> GetAccessToken()
        {
            var token =
                await HttpContext.GetTokenAsync(
                    "access_token");

            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException(
                    "Access token não encontrado.");
            }

            return token;
        }
    }
}

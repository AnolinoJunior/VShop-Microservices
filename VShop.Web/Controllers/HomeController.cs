
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Security.Claims;
using Vshop.Web.Models;
using Vshop.Web.Services.Contracts;
using VShop.Web.Models;
using VShop.Web.Services.Contracts;

namespace VShop.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IProductService _productService;
        private readonly ICartService _cartService;

        public HomeController(
            ILogger<HomeController> logger,
            IProductService productService,
            ICartService cartService)
        {
            _logger = logger;
            _productService = productService;
            _cartService = cartService;
        }

        // =====================================
        // 1 - CATÁLOGO DE PRODUTOS
        // =====================================

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var token = await HttpContext.GetTokenAsync(
                "access_token");

            var products = await _productService.GetAllProducts(
                token ?? string.Empty);

            if (products == null)
            {
                return View("Error");
            }

            return View(products);
        }

        // =====================================
        // 2 - DETALHES DO PRODUTO
        // =====================================

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ProductDetails(int id)
        {
            if (id <= 0)
            {
                return BadRequest();
            }

            var token = await HttpContext.GetTokenAsync(
                "access_token");

            var product = await _productService.FindProductById(
                id,
                token ?? string.Empty);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // =====================================
        // 3 - ADICIONAR PRODUTO AO CARRINHO
        // =====================================

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProductDetailsPost(
            int id,
            int quantity = 1)
        {
            if (id <= 0 || quantity <= 0)
            {
                return BadRequest();
            }

            // Obtém o identificador do usuário
            // autenticado no IdentityServer.
            var userId =
                User.FindFirstValue("sub")
                ?? User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge(
                    OpenIdConnectDefaults.AuthenticationScheme);
            }

            // Obtém o access token do usuário.
            var token = await HttpContext.GetTokenAsync(
                "access_token");

            if (string.IsNullOrWhiteSpace(token))
            {
                return Challenge(
                    OpenIdConnectDefaults.AuthenticationScheme);
            }

            // Busca os dados atualizados do produto.
            var product = await _productService.FindProductById(
                id,
                token);

            if (product == null)
            {
                return NotFound();
            }

            // Monta o carrinho que será enviado
            // para o microsserviço CartApi.
            var cartVM = new CartViewModel
            {
                CartHeader = new CartHeaderViewModel
                {
                    UserId = userId
                },

                CartItems = new List<CartItemViewModel>
                {
                    new CartItemViewModel
                    {
                        ProductId = product.ProductId,
                        Product = product,
                        Quantity = quantity
                    }
                }
            };

            // Envia o produto para a CartApi.
            var updatedCart =
                await _cartService.AddItemToCartAsync(
                    cartVM,
                    token);

            if (updatedCart == null)
            {
                _logger.LogWarning(
                    "Falha ao adicionar o produto {ProductId} " +
                    "ao carrinho do usuário {UserId}.",
                    id,
                    userId);

                TempData["ErrorMessage"] =
                    "Não foi possível adicionar o produto " +
                    "ao carrinho. Tente novamente.";

                return RedirectToAction(
                    nameof(ProductDetails),
                    new { id });
            }

            TempData["SuccessMessage"] =
                "Produto adicionado ao carrinho com sucesso!";

            return RedirectToAction(
                nameof(ProductDetails),
                new { id });
        }

        // =====================================
        // 4 - PRIVACIDADE
        // =====================================

        [HttpGet]
        public IActionResult Privacy()
        {
            return View();
        }

        // =====================================
        // 5 - LOGIN
        // =====================================

        [HttpGet]
        [Authorize]
        public IActionResult Login()
        {
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult CreateAccount()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction(nameof(Index));

            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Action(nameof(Index), "Home")
            };
            properties.Items["vshop_register"] = "1";
            return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
        }

        // =====================================
        // 6 - LOGOUT
        // =====================================

        [HttpGet]
        public IActionResult Logout()
        {
            return SignOut(
                CookieAuthenticationDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme);
        }

        // =====================================
        // 7 - ACESSO NEGADO
        // =====================================

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // =====================================
        // 8 - TRATAMENTO DE ERROS
        // =====================================

        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId =
                    Activity.Current?.Id
                    ?? HttpContext.TraceIdentifier
            });
        }
    }
}

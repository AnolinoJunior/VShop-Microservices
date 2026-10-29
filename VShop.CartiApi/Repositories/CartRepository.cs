
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using VShop.CartiApi.Context;
using VShop.CartiApi.DTOs;
using VShop.CartiApi.Models;
using VShop.CartiApi.Rules;

namespace VShop.CartiApi.Repositories
{
    public class CartRepository : ICartRepository
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CartRepository(
            AppDbContext context,
            IMapper mapper,
            IHttpClientFactory httpClientFactory,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _mapper = mapper;
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
        }

        // =====================================
        // 1 - BUSCAR CARRINHO PELO USUÁRIO
        // =====================================

        public async Task<CartDTO> GetCartByUserIdAsync(
            string userId)
        {
            ValidateUserId(userId);

            var cartHeader = await _context.CartHeaders
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cartHeader == null)
            {
                return _mapper.Map<CartDTO>(new Cart());
            }

            var cartItems = await _context.CartItems
                .AsNoTracking()
                .Include(c => c.Product)
                .Where(c => c.CartHeaderId == cartHeader.Id)
                .ToListAsync();

            var cart = new Cart
            {
                CartHeader = cartHeader,
                CartItems = cartItems
            };

            return _mapper.Map<CartDTO>(cart);
        }

        // =====================================
        // 2 - REMOVER PRODUTO DO CARRINHO
        // =====================================

        public async Task<bool> DeleteItemFromCartAsync(
            string userId,
            int productId)
        {
            ValidateUserId(userId);

            if (productId <= 0)
            {
                return false;
            }

            var cartHeader = await _context.CartHeaders
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cartHeader == null)
            {
                return false;
            }


            var cartItem = await _context.CartItems
                .FirstOrDefaultAsync(c =>
                    c.CartHeaderId == cartHeader.Id &&
                    c.ProductId == productId);

            if (cartItem == null)
            {
                return false;
            }

            _context.CartItems.Remove(cartItem);

            // Verifica se existem outros produtos.
            var hasOtherItems = await _context.CartItems
                .AnyAsync(c =>
                    c.CartHeaderId == cartHeader.Id &&
                    c.Id != cartItem.Id);

            // Remove o cabeçalho se o carrinho ficar vazio.
            if (!hasOtherItems)
            {
                _context.CartHeaders.Remove(cartHeader);
            }

            await _context.SaveChangesAsync();

            return true;
        }

        // =====================================
        // ATUALIZAR QUANTIDADE DE UM PRODUTO
        // =====================================

        public async Task<bool> UpdateItemQuantityAsync(
            string userId,
            int productId,
            int quantity)
        {
            ValidateUserId(userId);

            if (productId <= 0 || quantity < 1)
            {
                return false;
            }

            var cartHeader = await _context.CartHeaders
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cartHeader == null)
            {
                return false;
            }

            var cartItem = await _context.CartItems
                .FirstOrDefaultAsync(c =>
                    c.CartHeaderId == cartHeader.Id &&
                    c.ProductId == productId);

            if (cartItem == null)
            {
                return false;
            }

            // Substitui a quantidade, sem somar à anterior.
            cartItem.Quantity = quantity;

            await _context.SaveChangesAsync();

            return true;
        }

        // =====================================
        // 3 - LIMPAR CARRINHO
        // =====================================

        public async Task<bool> CleanCartAsync(string userId)
        {
            ValidateUserId(userId);

            var cartHeader = await _context.CartHeaders
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cartHeader == null)
            {
                return false;
            }

            // O relacionamento possui Cascade Delete.
            // Remover o cabeçalho também remove seus itens.
            _context.CartHeaders.Remove(cartHeader);

            await _context.SaveChangesAsync();

            return true;
        }

        // =====================================
        // 4 - ADICIONAR PRODUTO AO CARRINHO
        // =====================================

        public async Task<CartDTO> UpdateCartAsync(
            CartDTO cartDto)
        {
            ArgumentNullException.ThrowIfNull(cartDto);

            var cart = _mapper.Map<Cart>(cartDto);

            if (cart == null || cart.CartHeader == null)
            {
                throw new ArgumentException(
                    "O cabeçalho do carrinho é obrigatório.",
                    nameof(cartDto));
            }

            var userId = cart.CartHeader.UserId;

            ValidateUserId(userId);

            // A implementação recebe um produto por chamada.
            var cartItem = cart.CartItems?.FirstOrDefault();

            if (cartItem == null)
            {
                throw new ArgumentException(
                    "O carrinho precisa possuir pelo menos um item.",
                    nameof(cartDto));
            }

            if (cartItem.ProductId <= 0)
            {
                throw new ArgumentException(
                    "O ProductId deve ser maior que zero.",
                    nameof(cartDto));
            }

            if (cartItem.Quantity <= 0)
            {
                throw new ArgumentException(
                    "A quantidade deve ser maior que zero.",
                    nameof(cartDto));
            }

            // Evita operações parcialmente gravadas.
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            // Garante que o produto exista no banco da CartApi.
            await SaveProductInDataBase(cartItem);

            // Busca o carrinho existente do usuário.
            var existingHeader = await _context.CartHeaders
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (existingHeader == null)
            {
                await CreateCartHeaderAndItems(
                    cart.CartHeader,
                    cartItem);
            }
            else
            {
                await UpdateQuantityAndItems(
                    existingHeader,
                    cartItem);
            }

            await transaction.CommitAsync();

            // Retorna o carrinho atualizado.
            return await GetCartByUserIdAsync(userId);
        }

        // =====================================
        // 5 - APLICAR CUPOM
        // =====================================

        public async Task<bool> ApplyCouponAsync(
            string userId,
            string couponCode)
        {
            ValidateUserId(userId);

            if (string.IsNullOrWhiteSpace(couponCode))
            {
                return false;
            }

            var normalizedCoupon = couponCode.Trim();

            if (normalizedCoupon.Length > 50)
            {
                return false;
            }

            var token = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var client = _httpClientFactory.CreateClient("DiscountApi");
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/coupon/validate/{Uri.EscapeDataString(normalizedCoupon)}");
            request.Headers.TryAddWithoutValidation("Authorization", token);

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException("A DiscountApi está indisponível.", ex);
            }

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
            {
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"A DiscountApi retornou {(int)response.StatusCode} ({response.StatusCode}).");
            }

            var cartHeader = await _context.CartHeaders
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cartHeader == null)
            {
                return false;
            }

            cartHeader.CouponCode = normalizedCoupon;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<decimal?> GetSubtotalAsync(string userId)
        {
            ValidateUserId(userId);
            var headerId = await _context.CartHeaders.Where(c => c.UserId == userId)
                .Select(c => (int?)c.Id).FirstOrDefaultAsync();
            if (headerId == null) return null;
            return await _context.CartItems.Where(i => i.CartHeaderId == headerId.Value)
                .Join(_context.Products, i => i.ProductId, p => p.ProductId, (i, p) => p.Price * i.Quantity)
                .SumAsync();
        }

        public async Task<decimal?> GetVerifiedSubtotalAsync(string userId)
        {
            ValidateUserId(userId);
            var headerId = await _context.CartHeaders.Where(c => c.UserId == userId)
                .Select(c => (int?)c.Id).FirstOrDefaultAsync();
            if (headerId is null) return null;
            var items = await _context.CartItems.AsNoTracking().Where(i => i.CartHeaderId == headerId.Value)
                .Select(i => new { i.ProductId, i.Quantity }).ToListAsync();
            if (items.Count == 0 || items.Any(i => i.Quantity <= 0)) return null;

            var client = _httpClientFactory.CreateClient("ProductApi");
            decimal subtotal = 0;
            foreach (var item in items)
            {
                ProductDTO? product;
                try { product = await client.GetFromJsonAsync<ProductDTO>($"api/products/{item.ProductId}"); }
                catch (HttpRequestException ex) { throw new InvalidOperationException("Não foi possível consultar o preço na ProductApi.", ex); }
                if (product is null || product.ProductId != item.ProductId || product.Price < 0)
                    throw new InvalidOperationException("O produto não possui preço válido na ProductApi.");
                subtotal = checked(subtotal + product.Price * item.Quantity);
            }
            return subtotal;
        }

        public async Task<OrderSnapshotDTO?> GetOrderSnapshotAsync(string userId)
        {
            ValidateUserId(userId);
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var header = await _context.CartHeaders.AsNoTracking().FirstOrDefaultAsync(c =>
                EF.Functions.Collate(c.UserId, "Latin1_General_100_BIN2") == userId);
            if (header is null || !string.Equals(header.UserId, userId, StringComparison.Ordinal)) return null;
            var items = await _context.CartItems.AsNoTracking().Where(i => i.CartHeaderId == header.Id)
                .OrderBy(i => i.ProductId).Select(i => new { i.ProductId, i.Quantity }).ToListAsync();
            await transaction.CommitAsync();
            if (items.Count == 0 || items.Any(i => i.ProductId <= 0 || i.Quantity <= 0)) return null;

            var client = _httpClientFactory.CreateClient("ProductApi");
            var snapshot = new List<OrderSnapshotItemDTO>();
            foreach (var item in items)
            {
                ProductDTO? product;
                try { product = await client.GetFromJsonAsync<ProductDTO>($"api/products/{item.ProductId}"); }
                catch (HttpRequestException ex) { throw new InvalidOperationException("Não foi possível consultar a ProductApi.", ex); }
                if (product is null || product.ProductId != item.ProductId || product.Price < 0 ||
                    string.IsNullOrWhiteSpace(product.Name) || product.Name.Length > 200 || product.Description?.Length > 1000)
                    throw new InvalidOperationException("A ProductApi retornou dados de produto inválidos.");
                snapshot.Add(new(item.ProductId, product.Name, product.Description, item.Quantity, product.Price));
            }
            return new OrderSnapshotDTO(string.IsNullOrWhiteSpace(header.CouponCode) ? null : header.CouponCode.Trim(), snapshot);
        }

        // =====================================
        // 6 - REMOVER CUPOM
        // =====================================

        public async Task<bool> DeleteCouponAsync(
            string userId)
        {
            ValidateUserId(userId);

            var cartHeader = await _context.CartHeaders
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cartHeader == null)
            {
                return false;
            }

            cartHeader.CouponCode = string.Empty;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<CheckoutHeaderDTO?> CheckoutAsync(CheckoutHeaderDTO checkout)
        {
            ArgumentNullException.ThrowIfNull(checkout);
            ValidateUserId(checkout.UserId);
            var header = await _context.CartHeaders.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == checkout.UserId);
            if (header == null) return null;
            var items = await _context.CartItems.AsNoTracking().Include(i => i.Product).Where(i => i.CartHeaderId == header.Id).ToListAsync();
            if (items.Count == 0 || items.Any(i => i.Product == null || i.Quantity <= 0)) return null;
            var subtotal = items.Sum(i => i.Product!.Price * i.Quantity);
            var discount = string.IsNullOrWhiteSpace(header.CouponCode) ? 0m : await GetValidatedCouponDiscountAsync(header.CouponCode.Trim());
            discount = CouponRules.CalculateDiscount(subtotal, discount);
            return new CheckoutHeaderDTO { UserId = checkout.UserId, FirstName = checkout.FirstName.Trim(), LastName = checkout.LastName.Trim(), Email = checkout.Email.Trim(), Telephone = checkout.Telephone.Trim(), CouponCode = header.CouponCode, SubtotalAmount = subtotal, DiscountAmount = discount, TotalAmount = Math.Max(0m, subtotal - discount), Demonstration = true };
        }

        private async Task<decimal> GetValidatedCouponDiscountAsync(string code)
        {
            var token = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(token)) return 0m;
            var client = _httpClientFactory.CreateClient("DiscountApi");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/coupon/validate/{Uri.EscapeDataString(code)}");
            request.Headers.TryAddWithoutValidation("Authorization", token);
            HttpResponseMessage response;
            try { response = await client.SendAsync(request); }
            catch (HttpRequestException ex) { throw new InvalidOperationException("A DiscountApi está indisponível.", ex); }
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest) return 0m;
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"A DiscountApi retornou {(int)response.StatusCode} ({response.StatusCode}).");
            var coupon = await response.Content.ReadFromJsonAsync<CouponResponse>();
            return coupon is { IsActive: true, DiscountAmount: > 0 } ? coupon.DiscountAmount : 0m;
        }

        // =====================================
        // MÉTODO AUXILIAR:
        // CRIAR CARRINHO E PRIMEIRO ITEM
        // =====================================

        private async Task CreateCartHeaderAndItems(
            CartHeader cartHeader,
            CartItem cartItem)
        {
            // Não reutiliza as navegações recebidas do DTO
            // para evitar inserções duplicadas pelo EF Core.
            cartItem.Product = null!;
            cartItem.CartHeader = null!;

            // O banco gera o ID do cabeçalho.
            cartHeader.Id = 0;

            _context.CartHeaders.Add(cartHeader);

            await _context.SaveChangesAsync();

            cartItem.Id = 0;
            cartItem.CartHeaderId = cartHeader.Id;

            _context.CartItems.Add(cartItem);

            await _context.SaveChangesAsync();
        }

        // =====================================
        // MÉTODO AUXILIAR:
        // ADICIONAR ITEM OU SOMAR QUANTIDADE
        // =====================================

        private async Task UpdateQuantityAndItems(
            CartHeader cartHeader,
            CartItem newItem)
        {
            var existingItem = await _context.CartItems
                .FirstOrDefaultAsync(c =>
                    c.CartHeaderId == cartHeader.Id &&
                    c.ProductId == newItem.ProductId);

            if (existingItem == null)
            {
                // Produto diferente: adiciona ao carrinho.
                newItem.Id = 0;
                newItem.CartHeaderId = cartHeader.Id;

                newItem.Product = null!;
                newItem.CartHeader = null!;

                _context.CartItems.Add(newItem);
            }
            else
            {
                // Mesmo produto: soma as quantidades.
                existingItem.Quantity = checked(
                    existingItem.Quantity + newItem.Quantity);
            }

            await _context.SaveChangesAsync();
        }

        // =====================================
        // MÉTODO AUXILIAR:
        // SALVAR PRODUTO NO BANCO DA CARTAPI
        // =====================================

        private async Task SaveProductInDataBase(
            CartItem cartItem)
        {
            var existingProduct = await _context.Products
                .AsNoTracking()
                .AnyAsync(p =>
                    p.ProductId == cartItem.ProductId);

            if (existingProduct)
            {
                return;
            }

            var product = cartItem.Product;

            if (product == null ||
                product.ProductId != cartItem.ProductId)
            {
                throw new ArgumentException(
                    "Os dados do produto não foram informados corretamente.");
            }

            // O ProductId vem da ProductApi.
            // Na CartApi ele não é gerado automaticamente.
            _context.Products.Add(product);

            await _context.SaveChangesAsync();
        }

        // =====================================
        // MÉTODO AUXILIAR:
        // VALIDAR IDENTIFICADOR DO USUÁRIO
        // =====================================

        private static void ValidateUserId(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException(
                    "O UserId é obrigatório.",
                    nameof(userId));
            }
        }
    }

    internal sealed class CouponResponse
    {
        public decimal DiscountAmount { get; set; }
        public bool IsActive { get; set; }
    }
}

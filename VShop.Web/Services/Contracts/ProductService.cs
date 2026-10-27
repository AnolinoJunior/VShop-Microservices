using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Vshop.Web.Models;
using Vshop.Web.Services.Contracts;

namespace Vshop.Web.Services
{
    public class ProductService : IProductService
    {
        private readonly IHttpClientFactory _clientFactory;

        private const string apiEndpoint = "/api/products/";

        private readonly JsonSerializerOptions _options;

        public ProductService(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;

            _options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        // GET - Todos os produtos
        public async Task<IEnumerable<ProductViewModel>?> GetAllProducts(
            string token)
        {
            var client =
                _clientFactory.CreateClient("ProductApi");

            PutTokenInHeaderAuth(token, client);

            using var response =
                await client.GetAsync(apiEndpoint);

            if (!response.IsSuccessStatusCode)
                return null;

            var apiResponse =
                await response.Content.ReadAsStreamAsync();

            return await JsonSerializer
                .DeserializeAsync<IEnumerable<ProductViewModel>>(
                    apiResponse,
                    _options);
        }

        // GET - Produto por ID
        public async Task<ProductViewModel?> FindProductById(
            int id,
            string token)
        {
            var client =
                _clientFactory.CreateClient("ProductApi");

            PutTokenInHeaderAuth(token, client);

            using var response =
                await client.GetAsync(apiEndpoint + id);

            if (!response.IsSuccessStatusCode)
                return null;

            var apiResponse =
                await response.Content.ReadAsStreamAsync();

            return await JsonSerializer
                .DeserializeAsync<ProductViewModel>(
                    apiResponse,
                    _options);
        }

        // POST - Criar produto
        public async Task<ProductViewModel?> CreateProduct(
            ProductViewModel productVM,
            string token)
        {
            var client =
                _clientFactory.CreateClient("ProductApi");

            PutTokenInHeaderAuth(token, client);

            var content =
                new StringContent(
                    JsonSerializer.Serialize(productVM),
                    Encoding.UTF8,
                    "application/json");

            using var response =
                await client.PostAsync(
                    apiEndpoint,
                    content);

            if (!response.IsSuccessStatusCode)
                return null;

            var apiResponse =
                await response.Content.ReadAsStreamAsync();

            return await JsonSerializer
                .DeserializeAsync<ProductViewModel>(
                    apiResponse,
                    _options);
        }

        // PUT - Atualizar produto
        public async Task<ProductViewModel?> UpdateProduct(
            ProductViewModel productVM,
            string token)
        {
            var client =
                _clientFactory.CreateClient("ProductApi");

            PutTokenInHeaderAuth(token, client);

            using var response =
                await client.PutAsJsonAsync(
                    apiEndpoint + productVM.ProductId,
                    productVM);

            if (!response.IsSuccessStatusCode)
                return null;

            return productVM;
        }

        // DELETE - Excluir produto
        public async Task<bool> DeleteProductById(
            int id,
            string token)
        {
            var client =
                _clientFactory.CreateClient("ProductApi");

            PutTokenInHeaderAuth(token, client);

            using var response =
                await client.DeleteAsync(
                    apiEndpoint + id);

            return response.IsSuccessStatusCode;
        }

        // Adiciona o JWT no Header Authorization
        private static void PutTokenInHeaderAuth(
            string token,
            HttpClient client)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    token);
        }
    }
}
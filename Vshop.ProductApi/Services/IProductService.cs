using Vshop.ProductApi.DTOs;

namespace Vshop.ProductApi.Services
{
    public interface IProductService
    {
        Task<IEnumerable<ProductDTO>> GetAllProducts();

        Task<ProductDTO> GetProductById(int id);

        Task AddProduct(ProductDTO product);

        Task UpdateProduct(int id, ProductDTO product);

        Task RemoveProduct(int id);
    }
}
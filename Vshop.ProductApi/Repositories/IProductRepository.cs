using Vshop.ProductApi.Models;

namespace Vshop.ProductApi.Repositories
{
    public interface IProductRepository
    {
        Task<IEnumerable<Product>> GetAll();

        Task<Product?> GetProductById(int id);

        Task<Product> CreateProduct(Product product);

        Task<Product> UpdateProduct(Product product);

        Task<Product> DeleteProduct(int id);
    }
}
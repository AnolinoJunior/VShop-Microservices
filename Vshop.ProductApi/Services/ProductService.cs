using AutoMapper;
using Vshop.ProductApi.DTOs;
using Vshop.ProductApi.Models;
using Vshop.ProductApi.Repositories;

namespace Vshop.ProductApi.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly IMapper _mapper;

        public ProductService(
            IProductRepository productRepository,
            IMapper mapper)
        {
            _productRepository = productRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ProductDTO>> GetAllProducts()
        {
            var productsEntity = await _productRepository.GetAll();

            return _mapper.Map<IEnumerable<ProductDTO>>(productsEntity);
        }

        public async Task<ProductDTO> GetProductById(int id)
        {
            var productEntity =
                await _productRepository.GetProductById(id);

            return _mapper.Map<ProductDTO>(productEntity);
        }

        public async Task AddProduct(ProductDTO productDto)
        {
            var productEntity =
                _mapper.Map<Product>(productDto);

            await _productRepository.CreateProduct(productEntity);

            productDto.ProductId = productEntity.ProductId;
        }

        public async Task UpdateProduct(int id, ProductDTO productDto)
        {
            var productEntity =
                _mapper.Map<Product>(productDto);

            productEntity.ProductId = id;

            await _productRepository.UpdateProduct(productEntity);
        }

        public async Task RemoveProduct(int id)
        {
            var productEntity =
                await _productRepository.GetProductById(id);

            await _productRepository.DeleteProduct(productEntity!.ProductId);
        }
    }
}
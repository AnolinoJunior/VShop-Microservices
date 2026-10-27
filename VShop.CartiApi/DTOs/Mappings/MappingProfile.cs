
using AutoMapper;
using VShop.CartiApi.DTOs;
using VShop.CartiApi.Models;

namespace VShop.CartiApi.DTOs.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<CartDTO, Cart>().ReverseMap();

            CreateMap<CartHeaderDTO, CartHeader>().ReverseMap();

            CreateMap<CartItemDTO, CartItem>().ReverseMap();

            CreateMap<ProductDTO, Product>().ReverseMap();
        }
    }
}

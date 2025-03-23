using Application.Features.Dashboard.Dtos;
using AutoMapper;
using Domain.Entities;

namespace Application.Features.Dashboard.Profiles;

public class DashboardMappingProfile : Profile
{
    public DashboardMappingProfile()
    {
        // Product to TopProductDto mapping
        CreateMap<Product, TopProductDto>()
            .ForMember(dest => dest.Count, opt => opt.Ignore()) // This will be set manually
            .ForMember(dest => dest.Image, opt => opt.MapFrom(src => 
                src.ProductImageFiles.FirstOrDefault(pif => pif.Showcase).Path))
            .ForMember(dest => dest.BrandName, opt => opt.MapFrom(src => src.Brand != null ? src.Brand.Name : null));

        // Category to RecentItemDto mapping
        CreateMap<Category, RecentItemDto>()
            .ForMember(dest => dest.Image, opt => opt.MapFrom(src => 
                src.CategoryImageFiles.FirstOrDefault().Path));

        // Brand to RecentItemDto mapping
        CreateMap<Brand, RecentItemDto>()
            .ForMember(dest => dest.Image, opt => opt.MapFrom(src => 
                src.BrandImageFiles.FirstOrDefault().Path));
    }
}
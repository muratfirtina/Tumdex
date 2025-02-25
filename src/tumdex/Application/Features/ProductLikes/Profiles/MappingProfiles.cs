using Application.Features.ProductLikes.Commands.AddProductLike;
using Application.Features.ProductLikes.Queries;
using Application.Features.ProductLikes.Queries.GetProductsUserLiked;
using AutoMapper;
using Core.Application.Responses;
using Core.Persistence.Paging;
using Domain;

namespace Application.Features.ProductLikes.Profiles;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        CreateMap<ProductLike, AddProductLikeCommand>().ReverseMap();
        CreateMap<ProductLike, AddProductLikeResponse>().ReverseMap();
        
        CreateMap<IPaginate<ProductLike>, GetListResponse<GetUserLikedProductsQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items.Select(pl => pl.Product)));

        CreateMap<Product, GetUserLikedProductsQueryResponse>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name))
            .ForMember(dest => dest.BrandName, opt => opt.MapFrom(src => src.Brand.Name))
            .ForMember(dest => dest.ShowcaseImage, opt => opt.MapFrom(src => 
                src.ProductImageFiles.FirstOrDefault(pif => pif.Showcase)));
    }
}
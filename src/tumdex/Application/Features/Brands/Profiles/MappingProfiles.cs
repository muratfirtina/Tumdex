using Application.Features.Brands.Commands.Create;
using Application.Features.Brands.Commands.Delete;
using Application.Features.Brands.Commands.Update;
using Application.Features.Brands.Dtos;
using Application.Features.Brands.Queries.GetBrandsByIds;
using Application.Features.Brands.Queries.GetByDynamic;
using Application.Features.Brands.Queries.GetById;
using Application.Features.Brands.Queries.GetList;
using AutoMapper;
using Core.Application.Responses;
using Core.Persistence.Paging;
using Domain;

namespace Application.Features.Brands.Profiles;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        CreateMap<Brand, BrandDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.BrandImage, opt => opt.Ignore()) // Storage service ile doldurulacak
            .ReverseMap();
        
        CreateMap<IPaginate<Brand>, GetListResponse<BrandDto>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items))
            .ForMember(dest => dest.Pages, opt => opt.MapFrom(src => src.Pages))
            .ForMember(dest => dest.Size, opt => opt.MapFrom(src => src.Size))
            .ForMember(dest => dest.Count, opt => opt.MapFrom(src => src.Count))
            .ForMember(dest => dest.HasNext, opt => opt.MapFrom(src => src.HasNext))
            .ForMember(dest => dest.HasPrevious, opt => opt.MapFrom(src => src.HasPrevious))
            .ForMember(dest => dest.Index, opt => opt.MapFrom(src => src.Index));
        CreateMap<Brand, GetAllBrandQueryResponse>()
            .ForMember(dest
                => dest.BrandImage, opt
                => opt.MapFrom(src =>
                    src.BrandImageFiles.FirstOrDefault()))
            .ForMember(dest => dest.ProductCount, opt
                => opt.MapFrom(src => src.Products != null ? src.Products.Count : 0));
        CreateMap<Brand, GetByIdBrandResponse>()
            .ForMember(dest => dest.BrandImage, opt => opt.MapFrom(src => src.BrandImageFiles.FirstOrDefault()));
        CreateMap<List<Brand>, GetListResponse<GetAllBrandQueryResponse>>()
            .ForMember(dest 
                => dest.Items, opt 
                => opt.MapFrom(src => src));
        CreateMap<IPaginate<Brand>, GetListResponse<GetAllBrandQueryResponse>>()
            .ForMember(dest 
                => dest.Items, opt 
                => opt.MapFrom(src => src.Items))
            .ReverseMap();

        CreateMap<Brand, GetListBrandByDynamicQueryResponse>()
            .ForMember(dest 
                => dest.BrandImage, opt 
                => opt.MapFrom(src => src.BrandImageFiles.FirstOrDefault()));


        CreateMap<List<Brand>, GetListResponse<GetListBrandByDynamicQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src));
        
        CreateMap<IPaginate<Brand>, GetListResponse<GetListBrandByDynamicQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items))
            .ReverseMap();

        CreateMap<IPaginate<Brand>, List<GetListBrandByDynamicQueryResponse>>();
        
        CreateMap<Brand, CreateBrandCommand>().ReverseMap();
        CreateMap<Brand, CreatedBrandResponse>()
            .ForMember(dest => dest.BrandImage, opt => opt.MapFrom(src => 
                src.BrandImageFiles != null && src.BrandImageFiles.Any() 
                    ? new BrandImageFileDto { Url = src.BrandImageFiles.First().Url } 
                    : null));
        
        CreateMap<Brand, UpdateBrandCommand>().ReverseMap();
        CreateMap<Brand, UpdatedBrandResponse>().ReverseMap();
        CreateMap<Brand, DeletedBrandResponse>().ReverseMap();
        CreateMap<BrandImageFile, BrandImageFileDto>()
            .ForMember(dest => dest.FileName, opt => opt.MapFrom(src => src.Name))
            .ReverseMap();
        
        CreateMap<List<Brand>, GetListResponse<GetBrandsByIdsQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src));

        CreateMap<Brand, GetBrandsByIdsQueryResponse>()
            .ForMember(dest => dest.BrandImage, opt => opt.MapFrom(src => src.BrandImageFiles.FirstOrDefault()));

    }
}
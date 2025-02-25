using Application.Features.Categories.Commands.Create;
using Application.Features.Categories.Commands.Delete;
using Application.Features.Categories.Commands.Update;
using Application.Features.Categories.Dtos;
using Application.Features.Categories.Queries.GetByDynamic;
using Application.Features.Categories.Queries.GetById;
using Application.Features.Categories.Queries.GetCategoriesByIds;
using Application.Features.Categories.Queries.GetList;
using Application.Features.Categories.Queries.GetMainCategories;
using Application.Features.Categories.Queries.GetSubCategoriesByBrandId;
using Application.Features.Categories.Queries.GetSubCategoriesByCategoryId;
using Application.Features.Features.Commands.Create;
using Application.Features.Features.Dtos;
using Application.Features.FeatureValues.Dtos;
using Application.Features.Products.Dtos;
using AutoMapper;
using Core.Application.Responses;
using Core.Persistence.Paging;
using Domain;

namespace Application.Features.Categories.Profiles;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        CreateMap<Category, GetAllCategoryQueryResponse>()
            .ForMember(dest => dest.SubCategories, opt => opt.MapFrom(src => src.SubCategories))
            .ForMember(dest => dest.ProductCount, opt
                => opt.MapFrom(src => src.Products != null ? src.Products.Count : 0))
            .ForMember(dest
                => dest.CategoryImage, opt
                => opt.MapFrom(src
                    => src.CategoryImageFiles.FirstOrDefault()));

        CreateMap<Category, GetListSubCategoryDto>()
            .ForMember(dest => dest.CategoryImage, opt => opt.MapFrom(src => src.CategoryImageFiles.FirstOrDefault()))
            .ForMember(dest => dest.SubCategories, opt => opt.MapFrom(src => src.SubCategories));
        CreateMap<Category, GetByIdCategoryResponse>()
            .ForMember(dest => dest.ParentCategoryName, opt => opt.MapFrom(src => src.ParentCategory.Name))
            .ForMember(dest => dest.SubCategories, opt => opt.MapFrom(src => src.SubCategories))
            .ForMember(dest => dest.Features, opt => opt.MapFrom(src => src.Features))
            .ForMember(dest => dest.FeatureValueProductCounts, opt => opt.Ignore())
            .ForMember(dest => dest.CategoryImage, opt => opt.MapFrom(src => src.CategoryImageFiles.FirstOrDefault()))
            .ReverseMap();

        CreateMap<List<Category>, GetListResponse<GetAllCategoryQueryResponse>>()
            .ForMember(dest
                => dest.Items, opt
                => opt.MapFrom(src => src));

        CreateMap<IPaginate<Category>, GetListResponse<GetAllCategoryQueryResponse>>().ReverseMap();

        CreateMap<CreateCategoryCommand, Category>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            //.ForMember(dest => dest.CategoryFeatures, opt => opt.Ignore())
            .ReverseMap();
        CreateMap<Category, CreatedCategoryResponse>()
            .ForMember(dest => dest.CategoryImage, opt => opt.MapFrom(src => src.CategoryImageFiles.FirstOrDefault()));

        CreateMap<Category, UpdateCategoryCommand>().ReverseMap();
        CreateMap<Category, UpdatedCategoryResponse>().ReverseMap();
        CreateMap<Category, DeletedCategoryResponse>().ReverseMap();

        CreateMap<Category, GetListCategoryByDynamicDto>().ReverseMap();
        CreateMap<Category, GetListResponse<GetListCategoryByDynamicDto>>().ReverseMap();

        CreateMap<Feature, FeatureDto>()
            .ForMember(dest => dest.FeatureValues, opt => opt.MapFrom(src => src.FeatureValues));
        CreateMap<FeatureValue, FeatureValueDto>();
        CreateMap<CreateCategoryFeatureDto, CategoryFeature>().ReverseMap();
        CreateMap<Category, CreateCategoryResponseDto>()
            //  .ForMember(dest => dest.Features, opt => opt.MapFrom(src => src.CategoryFeatures.Select(cf => cf.Feature)))
            .ReverseMap();

        CreateMap<Category, CategoryDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.Title))
            .ForMember(dest => dest.ParentCategoryId, opt => opt.MapFrom(src => src.ParentCategoryId))
            .ForMember(dest => dest.SubCategories, opt => opt.MapFrom(src => src.SubCategories))
            .ForMember(dest => dest.CategoryImage, opt => opt.Ignore()) // Storage service ile doldurulacak
            .ReverseMap();
        
        CreateMap<IPaginate<Category>, GetListResponse<CategoryDto>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items))
            .ForMember(dest => dest.Pages, opt => opt.MapFrom(src => src.Pages))
            .ForMember(dest => dest.Size, opt => opt.MapFrom(src => src.Size))
            .ForMember(dest => dest.Count, opt => opt.MapFrom(src => src.Count))
            .ForMember(dest => dest.HasNext, opt => opt.MapFrom(src => src.HasNext))
            .ForMember(dest => dest.HasPrevious, opt => opt.MapFrom(src => src.HasPrevious))
            .ForMember(dest => dest.Index, opt => opt.MapFrom(src => src.Index));

        CreateMap<List<Category>, GetListResponse<GetListCategoryByDynamicDto>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src));

        CreateMap<Category, GetListCategoryByDynamicDto>()
            .ForMember(dest => dest.SubCategories,
                opt => opt.MapFrom(src => src.SubCategories))
            .ForMember(dest => dest.Products, opt => opt.MapFrom(src => src.Products));

        CreateMap<IPaginate<Category>, GetListResponse<GetListCategoryByDynamicDto>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items))
            .ReverseMap();

        CreateMap<IPaginate<Category>, List<GetListCategoryByDynamicDto>>();

        CreateMap<CategoryImageFile, CategoryImageFileDto>()
            .ForMember(dest => dest.FileName, opt => opt.MapFrom(src => src.Name))
            .ReverseMap();

        CreateMap<Category, GetListCategoryByDynamicDto>();

        CreateMap<Category, GetMainCategoriesResponse>()
            .ForMember(dest => dest.ProductCount, opt => opt.MapFrom(src => 
                (src.Products != null ? src.Products.Count : 0) + 
                (src.SubCategories != null ? src.SubCategories.Sum(sc => sc.Products != null ? sc.Products.Count : 0) : 0)))
            .ForMember(dest => dest.CategoryImage, opt => opt.MapFrom(src => src.CategoryImageFiles.FirstOrDefault()));
        
        CreateMap<List<Category>, GetListResponse<GetMainCategoriesResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src));

        CreateMap<IPaginate<Category>, GetListResponse<GetMainCategoriesResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items))
            .ReverseMap();

        CreateMap<List<Category>, GetListResponse<GetCategoriesByIdsQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src))
            .ForMember(dest => dest.Count, opt => opt.MapFrom(src => src.Count))
            .ForMember(dest => dest.Pages, opt => opt.MapFrom(src => 1))
            .ForMember(dest => dest.Index, opt => opt.MapFrom(src => 0))
            .ForMember(dest => dest.Size, opt => opt.MapFrom(src => src.Count))
            .ForMember(dest => dest.HasNext, opt => opt.MapFrom(src => false))
            .ForMember(dest => dest.HasPrevious, opt => opt.MapFrom(src => false));

        CreateMap<Category, GetCategoriesByIdsQueryResponse>()
            .ForMember(dest => dest.CategoryImage, opt => opt.MapFrom(src => src.CategoryImageFiles.FirstOrDefault()))
            .ForMember(dest => dest.SubCategories, opt => opt.MapFrom(src => src.SubCategories));

        CreateMap<List<Category>, GetListResponse<GetSubCategoriesByCategoryIdQueryReponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src));
        CreateMap<Category, GetSubCategoriesByCategoryIdQueryReponse>()
            .ForMember(dest => dest.CategoryImage, opt => opt.MapFrom(src => src.CategoryImageFiles.FirstOrDefault()));

        CreateMap<List<Category>, GetListResponse<GetSubCategoriesByBrandIdQueryReponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src));
        CreateMap<Category, GetSubCategoriesByBrandIdQueryReponse>()
            .ForMember(dest => dest.CategoryImage, opt => opt.MapFrom(src => src.CategoryImageFiles.FirstOrDefault()));

    }

}

using Application.Features.Categories.Queries.GetById;
using Application.Features.Products.Commands.Create;
using Application.Features.Products.Commands.Delete;
using Application.Features.Products.Commands.Update;
using Application.Features.Products.Dtos;
using Application.Features.Products.Dtos.FilterDto;
using Application.Features.Products.Queries.GetBestSelling;
using Application.Features.Products.Queries.GetByDynamic;
using Application.Features.Products.Queries.GetById;
using Application.Features.Products.Queries.GetList;
using Application.Features.Products.Queries.GetMostLikedProducts;
using Application.Features.Products.Queries.GetMostViewed;
using Application.Features.Products.Queries.GetRandoms.GetRandomProducts;
using Application.Features.Products.Queries.GetRandoms.GetRandomProductsByProductId;
using Application.Features.Products.Queries.GetRandoms.GetRandomProductsForBrand;
using Application.Features.Products.Queries.SearchAndFilter;
using Application.Features.Products.Queries.SearchAndFilter.Filter;
using Application.Features.Products.Queries.SearchAndFilter.Search;
using AutoMapper;
using Core.Application.Responses;
using Core.Persistence.Paging;
using Domain;

namespace Application.Features.Products.Profiles;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        CreateMap<Product, CreateMultipleProductsCommand>()
            .ForMember(dest => dest.Products, opt => opt.MapFrom(src => new List<CreateProductCommand> { new CreateProductCommand { Name = src.Name, Sku = src.Sku } }))
            .ReverseMap();
        CreateMap<Product, CreateProductCommand>()
            .ForMember(dest => dest.Sku, opt => opt.MapFrom(src => src.Sku))
            .ReverseMap();
        CreateMap<Product, CreatedProductResponse>()
            .ForMember(dest => dest.CategoryId, opt => opt.MapFrom(src => src.CategoryId))
            .ForMember(dest => dest.BrandId, opt => opt.MapFrom(src => src.BrandId))
            .ForMember(dest => dest.FeatureValueIds, opt => opt.MapFrom(src => src.ProductFeatureValues.Select(pfv => pfv.FeatureValueId)))
            .ForMember(dest => dest.Sku, opt => opt.MapFrom(src => src.Sku))
            .ForMember(dest => dest.Images, opt => opt.MapFrom(src => src.ProductImageFiles))

            .ReverseMap();
        CreateMap<Product, GetListResponse<GetAllProductQueryResponse>>()
            .ForMember(dest 
                => dest.Items, opt 
                => opt.MapFrom(src => src));
        
        CreateMap<List<Product>, GetListResponse<GetAllProductQueryResponse>>()
            .ForMember(dest 
                => dest.Items, opt 
                => opt.MapFrom(src => src));
        CreateMap<IPaginate<Product>, GetListResponse<GetAllProductQueryResponse>>()
            .ReverseMap();
        
       
        CreateMap<List<Product>, GetListResponse<GetListProductByDynamicDto>>()
            .ForMember(dest 
                => dest.Items, opt 
                => opt.MapFrom(src => src));
        CreateMap<IPaginate<Product>, GetListResponse<GetListProductByDynamicDto>>()
            .ReverseMap();
        
        CreateMap<Product, GetAllProductQueryResponse>()
            .ForMember(dest 
                => dest.CategoryName, opt 
                => opt.MapFrom(src => src.Category.Name))
            .ForMember(dest 
                => dest.BrandName, opt 
                => opt.MapFrom(src => src.Brand.Name))
            .ForMember(dest 
                => dest.ShowcaseImage, opt 
                => opt.MapFrom(src => 
                src.ProductImageFiles.FirstOrDefault(pif => pif.Showcase)))
            .ReverseMap();
        
        CreateMap<Product, GetListProductByDynamicDto>()
            .ForMember(dest 
                => dest.CategoryName, opt 
                => opt.MapFrom(src => src.Category.Name))
            .ForMember(dest 
                => dest.BrandName, opt 
                => opt.MapFrom(src => src.Brand.Name))
            .ForMember(dest 
                => dest.ShowcaseImage, opt 
                => opt.MapFrom(src => 
                src.ProductImageFiles.FirstOrDefault(pif => pif.Showcase)))
            .ReverseMap();
        
        CreateMap<Product, GetByIdProductResponse>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : null))
            .ForMember(dest => dest.BrandName, opt => opt.MapFrom(src => src.Brand != null ? src.Brand.Name : null))
            .ForMember(dest => dest.ProductFeatureValues, opt => opt.MapFrom(src => src.ProductFeatureValues
                .Where(pfv => pfv.FeatureValue != null && pfv.FeatureValue.Feature != null)
                .Select(pfv => new ProductFeatureValueDto
                {
                    FeatureId = pfv.FeatureValue.Feature.Id,
                    FeatureName = pfv.FeatureValue.Feature.Name,
                    FeatureValueId = pfv.FeatureValue.Id,
                    FeatureValueName = pfv.FeatureValue.Name
                })))
            .ReverseMap();
        
        CreateMap<Product, UpdateProductCommand>()
            .ForMember(dest => dest.BrandId, opt => opt.MapFrom(src => src.Brand.Id))
            .ForMember(dest => dest.CategoryId, opt => opt.MapFrom(src => src.Category.Id))
            .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.Title))  // ProductTitle alanını doğrudan eşliyoruz
            .ReverseMap()
            .ForMember(dest => dest.Brand, opt => opt.Ignore())
            .ForMember(dest => dest.Category, opt => opt.Ignore());

        CreateMap<Product, UpdatedProductResponse>()
          .ReverseMap();

        CreateMap<CreateMultipleProductDto, Product>()
            .ForMember(dest => dest.ProductImageFiles, opt => opt.Ignore())
            .ForMember(dest => dest.Category, opt => opt.Ignore())
            .ForMember(dest => dest.Brand, opt => opt.Ignore())
            .ReverseMap();
        
        CreateMap<Product, DeletedProductResponse>();

        CreateMap<Product, RelatedProductDto>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name))
            .ForMember(dest => dest.BrandName, opt => opt.MapFrom(src => src.Brand.Name))
            .ForMember(dest => dest.ProductFeatureValues, opt => opt.MapFrom(src => src.ProductFeatureValues))
            .ForMember(dest => dest.ShowcaseImage, opt => opt.MapFrom(src => src.ProductImageFiles.FirstOrDefault(pif => pif.Showcase == true)))
            .ReverseMap();

        CreateMap<ProductFeatureValue, ProductFeatureValueDto>()
            .ForMember(dest => dest.FeatureId, opt => opt.MapFrom(src => src.FeatureValue.Feature.Id))
            .ForMember(dest => dest.FeatureName, opt => opt.MapFrom(src => src.FeatureValue.Feature.Name))
            .ForMember(dest => dest.FeatureValueId, opt => opt.MapFrom(src => src.FeatureValue.Id))
            .ForMember(dest => dest.FeatureValueName, opt => opt.MapFrom(src => src.FeatureValue.Name))
            .ReverseMap();
        
        CreateMap<Product, ProductDto>()
            .ForMember(dest => dest.ShowcaseImage, opt => opt.MapFrom(src => 
                src.ProductImageFiles.FirstOrDefault(pif => pif.Showcase)));

        CreateMap<Product,SearchProductQueryResponse>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name))
            .ForMember(dest => dest.BrandName, opt => opt.MapFrom(src => src.Brand.Name))
            .ForMember(dest => dest.ShowcaseImage, opt => opt.MapFrom(src => src.ProductImageFiles.FirstOrDefault(pif => pif.Showcase)))
            .ReverseMap();
        
        CreateMap<IPaginate<Product>, GetListResponse<SearchProductQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items))
            .ReverseMap();
        
        CreateMap<Product, FilterProductQueryResponse>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name))
            .ForMember(dest => dest.BrandName, opt => opt.MapFrom(src => src.Brand.Name))
            .ForMember(dest => dest.ShowcaseImage, opt => opt.MapFrom(src => src.ProductImageFiles.FirstOrDefault(pif => pif.Showcase)))
            .ReverseMap();
        CreateMap<IPaginate<Product>, GetListResponse<FilterProductQueryResponse>>()
            .ReverseMap();
        
        CreateMap<FilterGroup, FilterGroupDto>()
            .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => src.DisplayName))
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type))
            .ForMember(dest => dest.Key, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.Options, opt => opt.MapFrom(src => src.Options))
            .ReverseMap();
        
        CreateMap<Product, GetRandomProductsByProductIdQueryResponse>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name))
            .ForMember(dest => dest.BrandName, opt => opt.MapFrom(src => src.Brand.Name))
            .ForMember(dest => dest.ShowcaseImage, opt => opt.MapFrom(src => src.ProductImageFiles.FirstOrDefault(pif => pif.Showcase)))
            .ReverseMap();
        CreateMap<List<Product>, GetListResponse<GetRandomProductsByProductIdQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src));
        
        CreateMap<IPaginate<Product>, GetListResponse<GetRandomProductsByProductIdQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items));
        CreateMap<Product, GetMostLikedProductQueryResponse>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name))
            .ForMember(dest => dest.BrandName, opt => opt.MapFrom(src => src.Brand.Name))
            .ForMember(dest => dest.ShowcaseImage, opt => opt.MapFrom(src => 
                src.ProductImageFiles.FirstOrDefault(pif => pif.Showcase)));

        CreateMap<List<Product>, GetListResponse<GetMostLikedProductQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src));
        
        
        CreateMap<IPaginate<Product>, GetListResponse<GetRandomProductsForBrandByProductIdQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items));


        CreateMap<Product, GetRandomProductsForBrandByProductIdQueryResponse>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name))
            .ForMember(dest => dest.BrandName, opt => opt.MapFrom(src => src.Brand.Name))
            .ForMember(dest => dest.ShowcaseImage, opt => opt.MapFrom(src =>
                src.ProductImageFiles.FirstOrDefault(pif => pif.Showcase)));
        
        CreateMap<List<Product>, GetListResponse<GetRandomProductsForBrandByProductIdQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src));

        // Domain.Product -> GetMostViewedProductQueryResponse eşlemesi
        CreateMap<Domain.Product, GetMostViewedProductQueryResponse>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name))
            .ForMember(dest => dest.BrandName, opt => opt.MapFrom(src => src.Brand.Name))
            .ForMember(dest => dest.ShowcaseImage, opt => opt.MapFrom(src =>
            src.ProductImageFiles.FirstOrDefault(pif => pif.Showcase)));
            

        // Listeyi GetListResponse türüne eşle
        CreateMap<List<Domain.Product>, GetListResponse<GetMostViewedProductQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src));
        
        CreateMap<Product, GetRandomProductsQueryResponse>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name))
            .ForMember(dest => dest.BrandName, opt => opt.MapFrom(src => src.Brand.Name))
            .ForMember(dest => dest.ShowcaseImage, opt => opt.MapFrom(src => 
                src.ProductImageFiles
                .FirstOrDefault(pif => pif.Showcase)));
        
        CreateMap<List<Product>, GetListResponse<GetRandomProductsQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src));
        
        CreateMap<Product, GetBestSellingProductsQueryResponse>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name))
            .ForMember(dest => dest.BrandName, opt => opt.MapFrom(src => src.Brand.Name))
            .ForMember(dest => dest.ShowcaseImage, opt => opt.MapFrom(src => 
                src.ProductImageFiles
                .FirstOrDefault(pif => pif.Showcase)));
        
        CreateMap<List<Product>, GetListResponse<GetBestSellingProductsQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src));
    }
    
}

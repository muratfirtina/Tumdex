using Application.Features.Features.Commands.Create;
using Application.Features.Features.Commands.Delete;
using Application.Features.Features.Commands.Update;
using Application.Features.Features.Dtos;
using Application.Features.Features.Queries.GetByDynamic;
using Application.Features.Features.Queries.GetById;
using Application.Features.Features.Queries.GetList;
using Application.Features.FeatureValues.Dtos;
using Application.Features.Products.Dtos;
using AutoMapper;
using Core.Application.Responses;
using Core.Persistence.Paging;
using Domain;

namespace Application.Features.Features.Profiles;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        CreateMap<Feature, GetAllFeatureQueryResponse>().ReverseMap();
        CreateMap<Feature, GetByIdFeatureResponse>().ReverseMap();
        CreateMap<List<Feature>, GetListResponse<GetAllFeatureQueryResponse>>()
            .ForMember(dest 
                => dest.Items, opt 
                => opt.MapFrom(src => src));
        CreateMap<IPaginate<Feature>, GetListResponse<GetAllFeatureQueryResponse>>().ReverseMap();
        
        CreateMap<CreateFeatureCommand, Feature>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            //.ForMember(dest => dest.CategoryFeatures, opt => opt.Ignore())
            .ReverseMap();

        CreateMap<Feature, CreatedFeatureResponse>()
            .ForMember(dest => dest.Categories, opt => opt.MapFrom(src => src.Categories))
            .ReverseMap();
        CreateMap<Feature, UpdateFeatureCommand>()
            //.ForMember(dest => dest.FeatureValueIds, opt => opt.MapFrom(src => src.FeatureValues.Select(fv => fv.Id).ToList()))
            //.ForMember(dest => dest.CategoryIds, opt => opt.MapFrom(src => src.Categories.Select(c => c.Id).ToList()))
            .ReverseMap();
        
        CreateMap<Feature, UpdatedFeatureResponse>().ReverseMap();
        CreateMap<Feature, DeletedFeatureResponse>().ReverseMap();
        CreateMap<Feature, GetListFeatureByDynamicDto>().ReverseMap();
        CreateMap<List<Feature>, GetListResponse<GetListFeatureByDynamicDto>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src));
        CreateMap<IPaginate<Feature>, GetListResponse<GetListFeatureByDynamicDto>>().ReverseMap();
        CreateMap<Feature, ProductFeatureDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.FeatureValues, opt => opt.MapFrom(src => src.FeatureValues.Select(fv =>
                new FeatureValueDto
                {
                    Id = fv.Id,
                    Name = fv.Name
                }).ToList()))
            .ReverseMap();
        
        CreateMap<FeatureValue, FeatureValueDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name));
        
        CreateMap<ProductFeatureDto, FeatureValue>()
            .ForMember(dest => dest.Name,
                opt => opt.MapFrom(src => src.FeatureValues.Select(f => f.Name).FirstOrDefault()))
            .ReverseMap();
        
        CreateMap<Feature, CreateFeatureDto>()
           // .ForMember(dest => dest.Categories, opt => opt.MapFrom(src => src.CategoryFeatures.Select(cf => cf.Category)))
            .ReverseMap();

        CreateMap<Feature, GetByIdFeatureResponse>()
            .ForMember(dest => dest.Categories, opt => opt.MapFrom(src => src.Categories))
            .ForMember(dest => dest.FeatureValues, opt => opt.MapFrom(src => src.FeatureValues))
            .ReverseMap();


    }
}
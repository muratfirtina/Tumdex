using Application.Features.ProductImageFiles.Dtos;
using AutoMapper;
using Domain;

namespace Application.Features.ProductImageFiles.Profiles;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        CreateMap<ProductImageFile, ProductImageFileDto>()
            .ForMember(dest => dest.Url, opt => opt.MapFrom(src => src.Url))
            .ForMember(dest => dest.Path, opt => opt.MapFrom(src => src.Path))
            .ForMember(dest => dest.FileName, opt => opt.MapFrom(src => src.Name))
            .ReverseMap();
    }
}
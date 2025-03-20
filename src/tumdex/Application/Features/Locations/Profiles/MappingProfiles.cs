using Application.Features.Locations.Dtos;
using AutoMapper;
using Core.Application.Responses;
using Domain.Entities;

namespace Application.Features.Locations.Profiles;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        // Country mappings
        CreateMap<Country, CountryDto>().ReverseMap();
        CreateMap<List<Country>, GetListResponse<CountryDto>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src));

        // City mappings
        CreateMap<City, CityDto>().ReverseMap();
        CreateMap<List<City>, GetListResponse<CityDto>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src));

        // District mappings
        CreateMap<District, DistrictDto>().ReverseMap();
        CreateMap<List<District>, GetListResponse<DistrictDto>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src));
    }
}
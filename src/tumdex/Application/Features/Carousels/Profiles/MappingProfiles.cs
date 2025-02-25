using Application.Features.Carousels.Commands.Create;
using Application.Features.Carousels.Commands.Update;
using Application.Features.Carousels.Dtos;
using Application.Features.Carousels.Queries.GetCarousel;
using AutoMapper;
using Core.Application.Responses;
using Core.Persistence.Paging;
using Domain;

namespace Application.Features.Carousels.Profiles;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        CreateMap<CreateCarouselCommand, Carousel>()
            .ForMember(dest => dest.CarouselImageFiles, opt => opt.Ignore());
        CreateMap<Carousel, CreatedCarouselResponse>()
            .ForMember(dest => dest.CarouselImageFiles, opt => opt.MapFrom(src => src.CarouselImageFiles))
            .ReverseMap();
        
        CreateMap<Carousel, UpdateCarouselCommand>().ReverseMap();
        CreateMap<Carousel, UpdatedCarouselResponse>().ReverseMap();
        
        CreateMap<CarouselImageFile, CarouselImageFileDto>()
            .ForMember(dest => dest.FileName, opt => opt.MapFrom(src => src.Name))
            .ReverseMap();
        
        CreateMap<List<Carousel>, GetListResponse<GetAllCarouselQueryResponse>>()
            .ForMember(dest 
                => dest.Items, opt 
                => opt.MapFrom(src => src));
        CreateMap<IPaginate<Carousel>, GetListResponse<GetAllCarouselQueryResponse>>()
            .ReverseMap();
        
        CreateMap<Carousel, GetAllCarouselQueryResponse>()
            .ForMember(dest 
                => dest.CarouselImageFiles, opt 
                => opt.MapFrom(src => src.CarouselImageFiles));
        
    }
}
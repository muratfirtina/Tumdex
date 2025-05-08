using Application.Features.Carousels.Commands.Create;
using Application.Features.Carousels.Commands.Update;
using Application.Features.Carousels.Dtos;
using Application.Features.Carousels.Queries.GetById;
using Application.Features.Carousels.Queries.GetCarousel;
using AutoMapper;
using Core.Application.Responses;
using Core.Persistence.Paging;
using Domain;
using Domain.Entities;

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
            // ID alanını açıkça map edelim
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.Order, opt => opt.MapFrom(src => src.Order))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive))
            .ForMember(dest => dest.CarouselImageFiles, opt => opt.MapFrom(src => src.CarouselImageFiles));
        
        
    }
}
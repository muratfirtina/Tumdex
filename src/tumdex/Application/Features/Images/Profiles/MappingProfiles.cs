using Application.Dtos.Image;
using AutoMapper;
using Domain;

namespace Application.Features.Images.Profiles;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<ImageVersion, ProcessedImageVersionDto>()
            .ForMember(dest => dest.Stream, opt => opt.Ignore());

        CreateMap<ProcessedImageVersionDto, ImageVersion>()
            .ForMember(dest => dest.ImageFile, opt => opt.Ignore())
            .ForMember(dest => dest.ImageFileId, opt => opt.Ignore());

        CreateMap<ImageFile, ImageSeoMetadataDto>()
            .ReverseMap();

        CreateMap<ImageProcessingOptionsDto, ImageFile>()
            .ForMember(dest => dest.Versions, opt => opt.Ignore());
    }
}
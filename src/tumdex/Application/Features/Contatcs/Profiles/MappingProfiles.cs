using Application.Features.Contatcs.Command;
using AutoMapper;
using Domain;

namespace Application.Features.Contatcs.Profiles;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        CreateMap<Contact, CreatedContactResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.Subject, opt => opt.MapFrom(src => src.Subject))
            .ForMember(dest => dest.Message, opt => opt.MapFrom(src => src.Message))
            .ForMember(dest => dest.CreatedDate, opt => opt.MapFrom(src => src.CreatedDate));

        CreateMap<CreateContactCommand, Contact>()
            .ForMember(dest => dest.Id, opt => opt.Ignore()) // ID otomatik oluşturulacak
            .ForMember(dest => dest.CreatedDate, opt => opt.Ignore()) // CreatedDate otomatik set edilecek
            .ForMember(dest => dest.UpdatedDate, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedDate, opt => opt.Ignore());
    }
}
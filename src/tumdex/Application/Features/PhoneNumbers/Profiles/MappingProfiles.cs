using Application.Features.PhoneNumbers.Commands.Create;
using Application.Features.PhoneNumbers.Commands.Update;
using Application.Features.PhoneNumbers.Dtos;
using Application.Features.PhoneNumbers.Queries.GetList;
using AutoMapper;
using Core.Application.Responses;
using Domain;

namespace Application.Features.PhoneNumbers.Profiles;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        CreateMap<PhoneNumber, PhoneNumberDto>().ReverseMap();
        CreateMap<PhoneNumber, CreatePhoneNumberDto>().ReverseMap();
        CreateMap<PhoneNumber, UpdatePhoneNumberDto>().ReverseMap();
        CreateMap<PhoneNumber, GetListPhoneNumberQueryResponse>();
        CreateMap<PhoneNumber, CreatedPhoneNumberCommandResponse>();
        CreateMap<PhoneNumber, UpdatedPhoneNumberCommandResponse>();
        CreateMap<CreatePhoneNumberCommand, CreatePhoneNumberDto>();
        CreateMap<UpdatePhoneNumberCommand, UpdatePhoneNumberDto>();
        CreateMap<List<PhoneNumber>, GetListResponse<GetListPhoneNumberQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src));
        
        CreateMap<CreatePhoneNumberCommand, CreatePhoneNumberDto>();
        CreateMap<UpdatePhoneNumberCommand, UpdatePhoneNumberDto>();
        
    }
}

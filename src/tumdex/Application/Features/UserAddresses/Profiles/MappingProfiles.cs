using Application.Features.UserAddresses.Commands;
using Application.Features.UserAddresses.Commands.Create;
using Application.Features.UserAddresses.Commands.Update;
using Application.Features.UserAddresses.Dtos;
using Application.Features.UserAddresses.Queries.GetList;
using AutoMapper;
using Core.Application.Responses;
using Domain;
using Domain.Entities;

namespace Application.Features.UserAddresses.Profiles;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        // Entity -> DTO Mapping
        CreateMap<UserAddress, UserAddressDto>();
        
        // DTO -> Response Mapping (Eksik olan mapping burası!)
        CreateMap<UserAddressDto, GetListUserAdressesQueryResponse>();
        
        // Entity -> Response Mapping
        CreateMap<UserAddress, GetListUserAdressesQueryResponse>();
        
        // Collection Mappings
        CreateMap<List<UserAddress>, GetListResponse<GetListUserAdressesQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src));
        CreateMap<List<UserAddressDto>, GetListResponse<GetListUserAdressesQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src));

        
        // CQRS Command Mappings
        CreateMap<CreateUserAddressCommand, CreateUserAddressDto>();
        CreateMap<UpdateUserAddressCommand, UpdateUserAddressDto>();
        CreateMap<UserAddress, CreatedUserAddressCommandResponse>();
        CreateMap<UserAddress, UpdatedUserAddressCommandResponse>();
    }
}
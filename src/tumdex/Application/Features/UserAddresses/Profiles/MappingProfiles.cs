using Application.Features.UserAddresses.Commands;
using Application.Features.UserAddresses.Commands.Create;
using Application.Features.UserAddresses.Commands.Update;
using Application.Features.UserAddresses.Dtos;
using Application.Features.UserAddresses.Queries.GetList;
using AutoMapper;
using Core.Application.Responses;
using Domain;

namespace Application.Features.UserAddresses.Profiles;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        CreateMap<UserAddress, UserAddressDto>().ReverseMap();
        CreateMap<UserAddress, CreateUserAddressDto>().ReverseMap();
        CreateMap<UserAddress, UpdateUserAddressDto>().ReverseMap();
        CreateMap<UserAddress, GetListUserAdressesQueryResponse>();
        CreateMap<UserAddress, CreatedUserAddressCommandResponse>();
        CreateMap<UserAddress, UpdatedUserAddressCommandResponse>();
        CreateMap<List<UserAddress>, GetListResponse<GetListUserAdressesQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src));
        
        CreateMap<CreateUserAddressCommand, CreateUserAddressDto>();
        CreateMap<UpdateUserAddressCommand, UpdateUserAddressDto>();
    }
}
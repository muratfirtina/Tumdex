using Application.Features.Roles.Queries.GetRoles;
using AutoMapper;
using Core.Application.Responses;
using Domain.Identity;

namespace Application.Features.Roles.Profiles;

public class MappingProfiles:Profile
{
    public MappingProfiles()
    {
        CreateMap<AppRole, GetAllRolesQueryResponse>();CreateMap<AppRole, GetAllRolesQueryResponse>();
        CreateMap<List<AppRole>, GetListResponse<GetAllRolesQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src))
            .ForMember(dest => dest.Count, opt => opt.MapFrom(src => src.Count));
    }
}
using Application.Features.Roles.Queries.GetUsersByRoleId;
using Application.Features.Users.Queries.GetAllUsers;
using Application.Features.Users.Queries.GetByDynamic;
using Application.Features.Users.Queries.GetCurrentUser;
using AutoMapper;
using Core.Application.Responses;
using Core.Persistence.Paging;
using Domain.Identity;

namespace Application.Features.Users.Profiles;

public class MappingProfiles:Profile
{
    public MappingProfiles()
    {
        CreateMap<AppUser, GetAllUsersQueryResponse>();
        CreateMap<AppUser, GetAllUsersQueryResponse>();
        CreateMap<List<AppUser>, GetListResponse<GetAllUsersQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src))
            .ForMember(dest => dest.Count, opt => opt.MapFrom(src => src.Count));

        CreateMap<AppUser, GetUsersByRoleIdQueryResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.UserName))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.NameSurname, opt => opt.MapFrom(src => src.NameSurname));

        CreateMap<List<AppUser>, GetListResponse<GetUsersByRoleIdQueryResponse>>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src))
            .ForMember(dest => dest.Count, opt => opt.MapFrom(src => src.Count));

        CreateMap<AppUser, GetListUserByDynamicQueryResponse>();

        CreateMap<IPaginate<AppUser>, GetListResponse<GetListUserByDynamicQueryResponse>>()
            .ReverseMap();

        CreateMap<List<AppUser>, GetListResponse<GetListUserByDynamicQueryResponse>>()
            .ForMember(dest
                => dest.Items, opt
                => opt.MapFrom(src => src));

        CreateMap<AppUser, GetCurrentUserQueryResponse>()
            .ReverseMap();
    }
}
using Application.Features.Newsletters.Commands.Subscribe;
using Application.Features.Newsletters.Commands.Unsubscribe;
using Application.Features.Newsletters.Dtos;
using AutoMapper;
using Domain;

namespace Application.Features.Newsletters.Profiles;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        CreateMap<Newsletter, SubscribeResponse>();
        CreateMap<Newsletter, UnsubscribeResponse>();
        CreateMap<Newsletter, NewsletterListDto>();
    }
}
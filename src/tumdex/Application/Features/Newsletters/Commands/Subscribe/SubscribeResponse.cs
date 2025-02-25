using Core.Application.Responses;

namespace Application.Features.Newsletters.Commands.Subscribe;

public class SubscribeResponse:IResponse
{
    public string Email { get; set; }
    public DateTime SubscriptionDate { get; set; }
}
using Core.Application.Responses;

namespace Application.Features.Newsletters.Commands.Unsubscribe;

public class UnsubscribeResponse:IResponse
{
    public string Email { get; set; }
    public DateTime? UnsubscriptionDate { get; set; }
}
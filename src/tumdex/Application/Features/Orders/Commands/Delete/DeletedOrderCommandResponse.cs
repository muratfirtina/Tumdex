using Core.Application.Responses;

namespace Application.Features.Orders.Commands.Delete;

public class DeletedOrderCommandResponse :IResponse
{
    public bool Success { get; set; }
}
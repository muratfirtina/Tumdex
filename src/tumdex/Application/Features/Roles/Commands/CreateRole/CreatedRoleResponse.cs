using Core.Application.Responses;

namespace Application.Features.Roles.Commands.CreateRole;

public class CreatedRoleResponse : IResponse
{
    public bool Succeeded { get; set; }
}
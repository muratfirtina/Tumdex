using Core.Application.Responses;

namespace Application.Features.Users.Queries.GetByDynamic;

public class GetListUserByDynamicQueryResponse : IResponse
{
    public string? Id { get; set; }
    public string? UserName { get; set; }
    public string? NameSurname { get; set; }
    public string? Email { get; set; }
}
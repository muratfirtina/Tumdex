using Core.Application.Responses;

namespace Application.Features.PhoneNumbers.Queries.GetList;

public class GetListPhoneNumberQueryResponse :IResponse
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Number { get; set; }
    public bool IsDefault { get; set; }
}
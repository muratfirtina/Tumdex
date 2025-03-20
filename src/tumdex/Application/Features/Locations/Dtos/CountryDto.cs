using Core.Application.Responses;

namespace Application.Features.Locations.Dtos;

public class CountryDto : IResponse
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string PhoneCode { get; set; }
}
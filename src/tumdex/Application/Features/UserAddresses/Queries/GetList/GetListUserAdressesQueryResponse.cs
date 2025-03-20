using Application.Features.UserAddresses.Dtos;
using Core.Application.Responses;

namespace Application.Features.UserAddresses.Queries.GetList;

public class GetListUserAdressesQueryResponse:IResponse
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? CityName { get; set; }
    public string? DistrictName { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryName { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}
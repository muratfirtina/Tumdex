namespace Application.Features.UserAddresses.Dtos;

public class UserAddressDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public int? CityId { get; set; }
    public string? CityName { get; set; }
    public int? DistrictId { get; set; }
    public string? DistrictName { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryName { get; set; }
    public int? CountryId { get; set; }
    public bool IsDefault { get; set; }
}
namespace Application.Features.UserAddresses.Dtos;

public class CreateUserAddressDto
{
    public string Name { get; set; }
    public string AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public int? CountryId { get; set; }
    public int? CityId { get; set; }
    public int? DistrictId { get; set; }
    public string? PostalCode { get; set; }
    public bool IsDefault { get; set; }
}
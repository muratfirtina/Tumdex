namespace Application.Features.Orders.Dtos;

public class OrderAddressDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string City { get; set; }
    public string? State { get; set; }
    public string Country { get; set; }
}
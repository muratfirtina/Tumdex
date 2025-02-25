namespace Application.Features.PhoneNumbers.Dtos;

public class UpdatePhoneNumberDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Number { get; set; }
    public bool IsDefault { get; set; }
}
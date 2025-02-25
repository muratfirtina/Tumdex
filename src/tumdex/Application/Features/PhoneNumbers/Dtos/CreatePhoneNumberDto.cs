namespace Application.Features.PhoneNumbers.Dtos;

public class CreatePhoneNumberDto
{
    public string Name { get; set; }
    public string Number { get; set; }
    public bool IsDefault { get; set; }
}
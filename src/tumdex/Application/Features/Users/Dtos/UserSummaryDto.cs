using Application.Features.PhoneNumbers.Dtos;
using Application.Features.UserAddresses.Dtos;

namespace Application.Features.Users.Dtos;

public class UserSummaryDto
{
    public string Id { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public string NameSurname { get; set; }
    public UserAddressDto? DefaultAddress { get; set; }
    public PhoneNumberDto? DefaultPhone { get; set; }
    public List<UserAddressDto> Addresses { get; set; }
    public List<PhoneNumberDto> PhoneNumbers { get; set; }

    public UserSummaryDto()
    {
        Addresses = new List<UserAddressDto>();
        PhoneNumbers = new List<PhoneNumberDto>();
    }
}
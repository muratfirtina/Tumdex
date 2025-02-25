using Application.Dtos.User;
using Application.Features.PhoneNumbers.Dtos;
using Core.Persistence.Repositories;
using Domain;

namespace Application.Repositories;

public interface IPhoneNumberRepository: IAsyncRepository<PhoneNumber, string> , IRepository<PhoneNumber, string>
{
    Task<PhoneNumber> AddPhoneAsync(CreatePhoneNumberDto phoneDto);
    Task<PhoneNumber> UpdatePhoneAsync(UpdatePhoneNumberDto phoneDto);
    Task<bool> DeletePhoneAsync(string id);
    Task<IList<PhoneNumber>> GetUserPhonesAsync();
    Task<bool> SetDefaultPhoneAsync(string id);
}
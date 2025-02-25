using Domain;

namespace Application.Services;

public interface IContactService
{
    Task CreateAsync(Contact contact);
}
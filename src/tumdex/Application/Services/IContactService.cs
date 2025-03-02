using Domain;
using Domain.Entities;

namespace Application.Services;

public interface IContactService
{
    Task CreateAsync(Contact contact);
}
using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;

namespace Application.Repositories;

public interface IContactRepository : IAsyncRepository<Contact, string>, IRepository<Contact, string>
{
    
}
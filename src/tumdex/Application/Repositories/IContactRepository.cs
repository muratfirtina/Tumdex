using Core.Persistence.Repositories;
using Domain;

namespace Application.Repositories;

public interface IContactRepository : IAsyncRepository<Contact, string>, IRepository<Contact, string>
{
    
}
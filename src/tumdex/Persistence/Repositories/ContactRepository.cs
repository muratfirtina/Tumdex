using Application.Repositories;
using Core.Persistence.Repositories;
using Domain;
using Persistence.Context;

namespace Persistence.Repositories;

public class ContactRepository : EfRepositoryBase<Contact, string, TumdexDbContext>, IContactRepository
{
    public ContactRepository(TumdexDbContext context) : base(context)
    {
    }
}
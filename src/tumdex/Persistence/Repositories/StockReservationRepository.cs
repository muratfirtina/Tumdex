using Application.Repositories;
using Core.Persistence.Repositories;
using Domain;
using Microsoft.EntityFrameworkCore.Storage;
using Persistence.Context;

namespace Persistence.Repositories;

public class StockReservationRepository : EfRepositoryBase<StockReservation, string, TumdexDbContext>, IStockReservationRepository
{
    public StockReservationRepository(TumdexDbContext context) : base(context)
    {
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync()
    {
        return await Context.Database.BeginTransactionAsync();
    }
}
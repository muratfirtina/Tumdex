using Core.Persistence.Repositories;
using Domain;
using Domain.Entities;
using Microsoft.EntityFrameworkCore.Storage;

namespace Application.Repositories;

public interface IStockReservationRepository : IAsyncRepository<StockReservation, string>, IRepository<StockReservation, string>
{
    Task<IDbContextTransaction> BeginTransactionAsync();
    
}
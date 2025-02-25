namespace Application.Services;

public interface IStockReservationService
{
    Task<bool> CreateReservationAsync(string productId, string cartItemId, int quantity);
    Task<bool> ReleaseReservationAsync(string cartItemId);
    Task ReleaseExpiredReservationsAsync();
    Task<bool> HasActiveReservationAsync(string cartItemId);
    Task<int> GetReservedQuantityAsync(string productId);
}
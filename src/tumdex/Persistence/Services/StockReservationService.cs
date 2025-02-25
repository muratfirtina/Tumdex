using Application.Repositories;
using Application.Services;
using Domain;
using Microsoft.Extensions.Logging;

namespace Persistence.Services;

public class StockReservationService : IStockReservationService
{
    private readonly IProductRepository _productRepository;
    private readonly IStockReservationRepository _reservationRepository;
    private readonly ILogger<StockReservationService> _logger;
    private const int RESERVATION_MINUTES = 5;

    public StockReservationService(
        IProductRepository productRepository,
        IStockReservationRepository reservationRepository,
        ILogger<StockReservationService> logger)
    {
        _productRepository = productRepository;
        _reservationRepository = reservationRepository;
        _logger = logger;
    }

    public async Task<bool> CreateReservationAsync(string productId, string cartItemId, int quantity)
{
    using var transaction = await _reservationRepository.BeginTransactionAsync();
    try
    {
        var product = await _productRepository.GetAsync(p => p.Id == productId);
        if (product == null)
        {
            _logger.LogWarning($"Product not found during reservation: {productId}");
            return false;
        }

        var existingReservation = await _reservationRepository.GetAsync(
            r => r.CartItemId == cartItemId && r.IsActive);

        var totalReserved = await GetReservedQuantityAsync(productId);
        var availableStock = product.Stock - totalReserved + (existingReservation?.Quantity ?? 0);

        if (availableStock < quantity)
        {
            _logger.LogWarning(
                $"Insufficient stock for reservation. Product: {productId}, Requested: {quantity}, Available: {availableStock}");
            return false;
        }

        if (existingReservation != null)
        {
            // Update stock, keeping original ExpirationTime
            product.Stock += (existingReservation.Quantity - quantity);
            await _productRepository.UpdateAsync(product);

            existingReservation.Quantity = quantity;
            await _reservationRepository.UpdateAsync(existingReservation);

            _logger.LogInformation(
                $"Stock reservation updated. Product: {productId}, CartItem: {cartItemId}, Quantity: {quantity}");
        }
        else
        {
            var reservation = new StockReservation
            {
                ProductId = productId,
                CartItemId = cartItemId,
                Quantity = quantity,
                ExpirationTime = DateTime.UtcNow.AddMinutes(RESERVATION_MINUTES),
                IsActive = true
            };
            await _reservationRepository.AddAsync(reservation);

            product.Stock -= quantity;
            await _productRepository.UpdateAsync(product);

            _logger.LogInformation(
                $"Stock reservation created. Product: {productId}, CartItem: {cartItemId}, Quantity: {quantity}");
        }

        await transaction.CommitAsync();
        return true;
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        _logger.LogError(ex, $"Error creating stock reservation. Product: {productId}, CartItem: {cartItemId}");
        return false;
    }
}


    public async Task<bool> ReleaseReservationAsync(string cartItemId)
    {
        try
        {
            var reservation = await _reservationRepository.GetAsync(
                r => r.CartItemId == cartItemId && r.IsActive);

            if (reservation == null)
                return false;

            reservation.IsActive = false;
            await _reservationRepository.UpdateAsync(reservation);

            _logger.LogInformation($"Stock reservation released. CartItem: {cartItemId}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error releasing stock reservation. CartItem: {cartItemId}");
            return false;
        }
    }

    public async Task ReleaseExpiredReservationsAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            var expiredReservations = (await _reservationRepository.GetListAsync(
                r => r.IsActive && r.ExpirationTime < now
            )).Items;

            foreach (var reservation in expiredReservations)
            {
                var product = await _productRepository.GetAsync(p => p.Id == reservation.ProductId);

                if (product != null)
                {
                    product.Stock += reservation.Quantity;
                    await _productRepository.UpdateAsync(product);
                    _logger.LogInformation(
                        $"Expired stock reservation released. Product: {reservation.ProductId}, Quantity: {reservation.Quantity}, CartItem: {reservation.CartItemId}, OldStock:{product.Stock - reservation.Quantity}, NewStock: {product.Stock}");
                }

                reservation.IsActive = false;
                await _reservationRepository.UpdateAsync(reservation);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing expired stock reservations");
            throw;
        }
    }

    public async Task<bool> HasActiveReservationAsync(string cartItemId)
    {
        try
        {
            var reservation = await _reservationRepository.GetAsync(
                r => r.CartItemId == cartItemId &&
                     r.IsActive &&
                     r.ExpirationTime > DateTime.UtcNow);

            return reservation != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking active reservation. CartItem: {cartItemId}");
            return false;
        }
    }

    public async Task<int> GetReservedQuantityAsync(string productId)
    {
        try
        {
            var now = DateTime.UtcNow;
            var activeReservations = (await _reservationRepository.GetListAsync(
                r => r.ProductId == productId &&
                     r.IsActive &&
                     r.ExpirationTime > now
            )).Items;

            return activeReservations.Sum(r => r.Quantity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting reserved quantity. Product: {productId}");
            return 0;
        }
    }

    public async Task<List<StockReservation>> GetActiveReservationsForProductAsync(string productId)
    {
        try
        {
            var now = DateTime.UtcNow;
            var results = await _reservationRepository.GetListAsync(
                r => r.ProductId == productId &&
                     r.IsActive &&
                     r.ExpirationTime > now
            );
            return results.Items.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting active reservations. Product: {productId}");
            return new List<StockReservation>();
        }
    }

    public async Task<bool> ExtendReservationAsync(string cartItemId, int additionalMinutes = RESERVATION_MINUTES)
    {
        try
        {
            var reservation = await _reservationRepository.GetAsync(
                r => r.CartItemId == cartItemId && r.IsActive);

            if (reservation == null)
                return false;

            reservation.ExpirationTime = DateTime.UtcNow.AddMinutes(additionalMinutes);
            await _reservationRepository.UpdateAsync(reservation);

            _logger.LogInformation($"Stock reservation extended. CartItem: {cartItemId}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error extending stock reservation. CartItem: {cartItemId}");
            return false;
        }
    }
}
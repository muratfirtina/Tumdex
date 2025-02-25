using System.Diagnostics;
using Application.Abstraction.Services;
using Application.Events.OrderEvetns;
using Application.Repositories;
using Application.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Consumers
{
    public class CartUpdatedEventConsumer : IConsumer<CartUpdatedEvent>
    {
        private readonly ILogger<CartUpdatedEventConsumer> _logger;
        private readonly IMetricsService _metricsService;
        private readonly SemaphoreSlim _throttler;

        public CartUpdatedEventConsumer(
            ILogger<CartUpdatedEventConsumer> logger,
            IMetricsService metricsService)
        {
            _logger = logger;
            _metricsService = metricsService;
            _throttler = new SemaphoreSlim(100, 100); // Concurrent işlem limiti
        }

        public async Task Consume(ConsumeContext<CartUpdatedEvent> context)
        {
             var cartEvent = context.Message;
             var sw = Stopwatch.StartNew();
             try
            {
                if (!await _throttler.WaitAsync(TimeSpan.FromSeconds(5)))
                {
                     _logger.LogWarning("Cart update throttled for ProductId: {ProductId}", cartEvent.ProductId);
                    throw new ThrottlingException("Cart update is currently throttled");
                }

                _logger.LogInformation("Processing cart update for ProductId: {ProductId}, Quantity: {Quantity}, CartItemId: {CartItemId}",
                    cartEvent.ProductId, cartEvent.Quantity, cartEvent.CartItemId);

                _metricsService.RecordApiCall("cart-update", "POST", "v1");


                // Metrikler
                sw.Stop();
                _metricsService.RecordApiLatency("cart-update", "POST", sw.ElapsedMilliseconds);
                _metricsService.UpdateCartAbandonment("registered", 0);
                 _metricsService.RecordCheckoutDuration(
                        "registered",
                       "pending",
                       sw.ElapsedMilliseconds / 1000.0
                    );
             }
            catch (Exception ex)
            {
                    _logger.LogError(ex, "Error processing cart update for ProductId: {ProductId}", cartEvent.ProductId);
                 _metricsService.IncrementAlertCounter("cart_update_error", new Dictionary<string, string>
                 {
                    { "product_id", cartEvent.ProductId },
                  { "severity", "error" }
                 });

                 throw;
            }
           finally
           {
              _throttler.Release();
          }
       }
    }
}
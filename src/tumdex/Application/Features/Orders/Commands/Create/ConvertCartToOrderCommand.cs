using System.Text.Json;
using Application.Abstraction.Services;
using Application.Abstraction.Services.HubServices;
using Application.Events.OrderEvetns;
using Application.Extensions;
using Application.Features.Carts.Dtos;
using Application.Features.Orders.Dtos;
using Application.Repositories;
using Application.Storage;
using AutoMapper;
using Core.Application.Pipelines.Caching;
using Core.Application.Pipelines.Transaction;
using Domain;
using Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Orders.Commands.Create;

public class ConvertCartToOrderCommand : IRequest<ConvertCartToOrderCommandResponse>,ITransactionalRequest,ICacheRemoverRequest
{
    public string? AddressId { get; set; }
    public string? PhoneNumberId { get; set; }
    public string? Description { get; set; }
    
    public string CacheKey => "";
    public bool BypassCache => false;
    public string? CacheGroupKey => "Orders";

    public class ConvertCartToOrderCommandHandler : IRequestHandler<ConvertCartToOrderCommand, ConvertCartToOrderCommandResponse>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IOutboxRepository _outboxRepository;
        private readonly ILogger<ConvertCartToOrderCommandHandler> _logger;
        private readonly IOrderEmailService _orderEmailService;

        public ConvertCartToOrderCommandHandler(
            IOrderRepository orderRepository, 
            IOutboxRepository outboxRepository, 
            ILogger<ConvertCartToOrderCommandHandler> logger,
            IOrderEmailService orderEmailService)
        {
            _orderRepository = orderRepository;
            _outboxRepository = outboxRepository;
            _logger = logger;
            _orderEmailService = orderEmailService;
        }

        public async Task<ConvertCartToOrderCommandResponse> Handle(ConvertCartToOrderCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // 1. Sipariş oluşturma (Transaction içinde)
                (bool succeeded, OrderDto? orderDto) = await _orderRepository.ConvertCartToOrderAsync(
                    request.AddressId,
                    request.PhoneNumberId,
                    request.Description
                );

                if (!succeeded || orderDto == null)
                {
                    throw new Exception("Sepet siparişe dönüştürülemedi.");
                }

                // 2. E-posta göndermeyi dene
                bool emailSent = false;
                try
                {
                    await _orderEmailService.SendCreatedOrderEmailAsync(
                        orderDto.Email,
                        orderDto.OrderCode,
                        request.Description ?? string.Empty,
                        orderDto.UserAddress,
                        orderDto.OrderDate,
                        orderDto.UserName,
                        orderDto.OrderItems,
                        orderDto.TotalPrice
                    );
                    
                    // E-posta başarıyla gönderildi
                    emailSent = true;
                    _logger.LogInformation("Sipariş onay e-postası başarıyla gönderildi. OrderId: {OrderId}", orderDto.OrderId);
                }
                catch (Exception mailEx)
                {
                    _logger.LogError(mailEx, "Sipariş onay e-postası gönderilemedi. OrderId: {OrderId}. Sipariş iptal ediliyor.", orderDto.OrderId);
                    
                    // E-posta gönderimi başarısız olursa, siparişi geri al
                    var cancelResult = await _orderRepository.CancelOrderAsync(orderDto.OrderId);
                    
                    if (!cancelResult)
                    {
                        _logger.LogError("Sipariş iptal edilemedi. OrderId: {OrderId}", orderDto.OrderId);
                    }
                    
                    // Kullanıcıya bilgi vermek için özel bir hata fırlat
                    throw new Exception("Sipariş onay e-postası gönderilemediği için işlem iptal edildi. Lütfen daha sonra tekrar deneyin veya müşteri hizmetleriyle iletişime geçin.", mailEx);
                }

                // 3. Event'i Outbox'a kaydet
                var outboxMessage = new OutboxMessage(
                    nameof(OrderCreatedEvent),
                    JsonSerializer.Serialize(new OrderCreatedEvent
                    {
                        OrderId = orderDto.OrderId,
                        OrderCode = orderDto.OrderCode,
                        OrderDate = orderDto.OrderDate,
                        Description = request.Description,
                        UserAddress = orderDto.UserAddress,
                        UserName = orderDto.UserName,
                        OrderItems = orderDto.OrderItems,
                        TotalPrice = orderDto.TotalPrice,
                        Email = orderDto.Email,
                        EmailSent = emailSent // E-posta durumunu belirt
                    })
                );

                await _outboxRepository.AddAsync(outboxMessage);

                return new ConvertCartToOrderCommandResponse
                {
                    OrderId = orderDto.OrderId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sepet siparişe dönüştürülürken hata oluştu");
                throw;
            }
        }
    }
    
}

using System.Text.Json;
using Application.Abstraction.Services;
using Application.Abstraction.Services.Email;
using Application.Abstraction.Services.HubServices;
using Application.Consts;
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

public class ConvertCartToOrderCommand : IRequest<ConvertCartToOrderCommandResponse>
{
    public string? AddressId { get; set; }
    public string? PhoneNumberId { get; set; }
    public string? Description { get; set; }
    public string CacheKey => "";
    public bool BypassCache => false;
    public string? CacheGroupKey => CacheGroups.CartsAndOrders;

    public class
        ConvertCartToOrderCommandHandler : IRequestHandler<ConvertCartToOrderCommand, ConvertCartToOrderCommandResponse>
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

        public async Task<ConvertCartToOrderCommandResponse> Handle(ConvertCartToOrderCommand request,
            CancellationToken cancellationToken)
        {
            try
            {
                // 1. Create order (within Transaction)
                (bool succeeded, OrderDto? orderDto) = await _orderRepository.ConvertCartToOrderAsync(
                    request.AddressId,
                    request.PhoneNumberId,
                    request.Description
                );

                if (!succeeded || orderDto == null)
                {
                    // Daha açıklayıcı hata mesajı
                    var errorMessage =
                        "Sipariş oluşturulamadı. Lütfen sepetinizi, adres ve telefon bilgilerinizi kontrol edin.";
                    _logger.LogError(
                        "Sipariş oluşturma başarısız. AddressId: {AddressId}, PhoneNumberId: {PhoneNumberId}",
                        request.AddressId, request.PhoneNumberId);
                    throw new Exception(errorMessage);
                }

                // 2. Try to send email
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

                    // Email sent successfully
                    emailSent = true;
                    _logger.LogInformation("Order confirmation email was successfully sent. OrderId: {OrderId}",
                        orderDto.OrderId);
                }
                catch (Exception mailEx)
                {
                    _logger.LogError(mailEx,
                        "Order confirmation email could not be sent. OrderId: {OrderId}. Canceling the order.",
                        orderDto.OrderId);

                    // If email sending fails, roll back the order
                    var cancelResult = await _orderRepository.CancelOrderAsync(orderDto.OrderId);

                    if (!cancelResult)
                    {
                        _logger.LogError("Order could not be canceled. OrderId: {OrderId}", orderDto.OrderId);
                    }

                    // Throw a custom error to inform the user
                    throw new Exception(
                        "The transaction was canceled because the order confirmation email could not be sent. Please try again later or contact customer service.",
                        mailEx);
                }

                // 3. Save the Event to Outbox
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
                        EmailSent = emailSent // Indicate email status
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
                _logger.LogError(ex, "An error occurred while converting cart to order");
                throw;
            }
        }
    }
}
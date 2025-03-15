using System.Text;
using Application.Abstraction.Services;
using Application.Abstraction.Services.Email;
using Application.Abstraction.Services.Utilities;
using Application.Features.Orders.Dtos;
using Application.Features.UserAddresses.Dtos;
using Application.Storage;
using Azure.Security.KeyVault.Secrets;
using Domain.Enum;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Mail;

/// <summary>
/// Sipariş bildirimleri için e-posta servisi
/// </summary>
public class OrderEmailService : BaseEmailService, IOrderEmailService
{
    protected override string ServiceType => "ORDER_EMAIL";
    protected override string ConfigPrefix => "OrderEmail";
    protected override string PasswordSecretName => "OrderEmailPassword";

    public OrderEmailService(
        ILogger<OrderEmailService> logger,
        ICacheService cacheService,
        IMetricsService metricsService,
        IStorageService storageService,
        SecretClient secretClient,
        IConfiguration configuration)
        : base(logger, cacheService, metricsService, storageService, secretClient, configuration)
    {
    }

    protected override async Task CheckRateLimit(string[] recipients)
    {
        foreach (var recipient in recipients)
        {
            var rateLimitKey = $"order_email_ratelimit_{recipient}_{DateTime.UtcNow:yyyyMMddHH}";
            var count = await _cacheService.GetCounterAsync(rateLimitKey);

            if (count >= 10) // Sipariş e-postaları için daha yüksek limit
                throw new Exception($"Email rate limit exceeded for recipient: {recipient}");

            await _cacheService.IncrementAsync(rateLimitKey, 1, TimeSpan.FromHours(1));
        }
    }

    protected override string GetEmailTitleColor()
    {
        return "#059669"; // Siparişler için yeşil renk
    }

    protected override string GetFooterMessage()
    {
        return "This is an automated order notification.<br>Please do not reply to this email.";
    }

    /// <summary>
    /// Sipariş oluşturma bildirimi gönderir
    /// </summary>
    public async Task SendCreatedOrderEmailAsync(
        string to,
        string orderCode,
        string orderDescription,
        UserAddressDto? orderAddress,
        DateTime orderCreatedDate,
        string userName,
        List<OrderItemDto> orderCartItems,
        decimal? orderTotalPrice)
    {
        try
        {
            var content = new StringBuilder();
            content.Append(BuildOrderConfirmationContent(
                userName,
                orderCode,
                orderDescription,
                orderAddress,
                orderCreatedDate,
                orderCartItems,
                orderTotalPrice));

            var emailBody = await BuildEmailTemplate(content.ToString(), "Order Confirmation");
            await SendEmailAsync(to, "Order Created ✓", emailBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send order confirmation email");
            throw;
        }
    }

    /// <summary>
    /// Sipariş güncelleme bildirimi gönderir
    /// </summary>
    public async Task SendOrderUpdateNotificationAsync(
        string to,
        string? orderCode,
        string? adminNote,
        OrderStatus? originalStatus,
        OrderStatus? updatedStatus,
        decimal? originalTotalPrice,
        decimal? updatedTotalPrice,
        List<OrderItemUpdateDto>? updatedItems)
    {
        try
        {
            var content = new StringBuilder();
            content.Append(BuildOrderUpdateContent(
                orderCode, adminNote, originalStatus, updatedStatus,
                originalTotalPrice, updatedTotalPrice, updatedItems));

            var emailBody = await BuildEmailTemplate(content.ToString(), "Order Update Notification");
            await SendEmailAsync(to, "Order Update Notification", emailBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send order update notification email");
            throw;
        }
    }

    /// <summary>
    /// Sipariş onay içeriği oluşturur
    /// </summary>
    private string BuildOrderConfirmationContent(
        string userName,
        string orderCode,
        string orderDescription,
        UserAddressDto? orderAddress,
        DateTime orderCreatedDate,
        List<OrderItemDto> orderCartItems,
        decimal? orderTotalPrice)
    {
        var sb = new StringBuilder();

        // Header
        sb.Append($@"
        <div style='background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;'>
            <p style='font-size: 16px; color: #333;'>Hello {userName},</p>
            <p style='color: #666;'>Your order has been successfully created.</p>
        </div>");

        // Order items table
        sb.Append(BuildOrderItemsTable(orderCartItems));

        // Order information
        sb.Append($@"
        <div style='margin-top: 30px; padding: 20px; background-color: #f8f9fa; border-radius: 5px;'>
            <h3 style='color: #333333; margin-bottom: 15px;'>Order Details</h3>
            <p style='color: #e53935;'><strong>Order Code: {orderCode}</strong></p>
            <p><strong>Order Date:</strong> {orderCreatedDate:dd.MM.yyyy HH:mm}</p>
            <p><strong>Delivery Address:</strong><br>{FormatAddress(orderAddress)}</p>
            <p><strong>Order Note:</strong><br>{orderDescription}</p>
            <p style='color: #059669;'><strong>Total Amount:</strong><br>${orderTotalPrice:N2}</p>
        </div>");

        return sb.ToString();
    }

    /// <summary>
    /// Sipariş öğeleri tablosu oluşturur
    /// </summary>
    private string BuildOrderItemsTable(List<OrderItemDto> items)
    {
        var sb = new StringBuilder();
        decimal totalAmount = 0;

        sb.Append(@"
        <table style='width: 100%; border-collapse: collapse; margin-top: 10px;'>
            <tr style='background-color: #333333; color: white;'>
                <th style='padding: 12px; text-align: left;'>Product</th>
                <th style='padding: 12px; text-align: right;'>Price</th>
                <th style='padding: 12px; text-align: center;'>Quantity</th>
                <th style='padding: 12px; text-align: right;'>Total</th>
                <th style='padding: 12px; text-align: center;'>Image</th>
            </tr>");

        foreach (var item in items)
        {
            var itemTotal = (item.Price ?? 0) * (item.Quantity ?? 0);
            totalAmount += itemTotal;

            string imageUrl = item.ShowcaseImage?.Url ?? "";

            sb.Append($@"
            <tr style='border-bottom: 1px solid #e0e0e0;'>
                <td style='padding: 12px;'>
                    <strong style='color: #333;'>{item.BrandName}</strong><br>
                    <span style='color: #666;'>{item.ProductName}</span>
                </td>
                <td style='padding: 12px; text-align: right;'>${item.Price:N2}</td>
                <td style='padding: 12px; text-align: center;'>{item.Quantity}</td>
                <td style='padding: 12px; text-align: right;'>${itemTotal:N2}</td>
                <td style='padding: 12px; text-align: center;'>
                    <img src='{imageUrl}' style='max-width: 80px; max-height: 80px; border-radius: 4px;'
                         alt='{item.ProductName}'/>
                </td>
            </tr>");
        }

        sb.Append($@"
        <tr style='background-color: #f8f9fa; color: #059669; font-weight: bold;'>
            <td colspan='3' style='padding: 12px; text-align: right;'>Total Amount:</td>
            <td colspan='2' style='padding: 12px; text-align: right;'>${totalAmount:N2}</td>
        </tr>");

        sb.Append("</table>");
        return sb.ToString();
    }

    /// <summary>
    /// Sipariş güncelleme içeriği oluşturur
    /// </summary>
    private string BuildOrderUpdateContent(
        string? orderCode,
        string? adminNote,
        OrderStatus? originalStatus,
        OrderStatus? updatedStatus,
        decimal? originalTotalPrice,
        decimal? updatedTotalPrice,
        List<OrderItemUpdateDto>? updatedItems)
    {
        var sb = new StringBuilder();

        // Header with order info
        sb.Append($@"
            <div style='background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;'>
                <p style='font-size: 16px; color: #333;'>Dear valued customer,</p>
                <p style='color: #666;'>We would like to inform you about updates to your order.</p>
            </div>

            <div style='background-color: #fff3cd; padding: 20px; border-radius: 5px; margin-bottom: 20px;'>
                <p style='color: #e53935;'><strong>Order Code: {orderCode}</strong></p>");

        // Show status change if both statuses exist and are different
        if (originalStatus.HasValue && updatedStatus.HasValue && originalStatus != updatedStatus)
        {
            sb.Append($@"
                <p style='color: #856404;'>
                    <strong>Order Status:</strong> Changed from <span style='color: #6c757d;'>{originalStatus}</span> 
                    to <span style='color: #28a745;'>{updatedStatus}</span>
                </p>");
        }

        // Admin note if exists
        if (!string.IsNullOrEmpty(adminNote))
        {
            sb.Append($@"<p style='color: #856404;'><strong>Admin Note:</strong> {adminNote}</p>");
        }

        sb.Append("</div>");

        // Updated items table if exists
        if (updatedItems?.Any() == true)
        {
            sb.Append(BuildUpdatedItemsTable(updatedItems, originalTotalPrice, updatedTotalPrice));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Güncellenen sipariş öğeleri tablosu oluşturur
    /// </summary>
    private string BuildUpdatedItemsTable(
        List<OrderItemUpdateDto> items,
        decimal? originalTotalPrice,
        decimal? updatedTotalPrice)
    {
        var sb = new StringBuilder();

        sb.Append(@"
        <table style='width: 100%; border-collapse: collapse; margin-top: 20px;'>
            <tr style='background-color: #333333; color: white;'>
                <th style='padding: 12px; text-align: left;'>Product</th>
                <th style='padding: 12px; text-align: right;'>Old Price</th>
                <th style='padding: 12px; text-align: right;'>New Price</th>
                <th style='padding: 12px; text-align: center;'>Quantity</th>
                <th style='padding: 12px; text-align: center;'>Lead Time</th>
                <th style='padding: 12px; text-align: right;'>Total</th>
                <th style='padding: 12px; text-align: center;'>Image</th>
            </tr>");

        foreach (var item in items)
        {
            if (item.UpdatedPrice.HasValue && item.Price.HasValue && item.Quantity.HasValue)
            {
                var itemTotal = item.UpdatedPrice.Value * item.Quantity.Value;
                var priceChange = item.UpdatedPrice > item.Price ? "color: #dc3545;" : "color: #28a745;";

                string imageUrl = item.ShowcaseImage?.Url ?? string.Empty;

                sb.Append($@"
                <tr style='border-bottom: 1px solid #e0e0e0;'>
                    <td style='padding: 12px;'>
                        <strong style='color: #333;'>{item.BrandName}</strong><br>
                        <span style='color: #666;'>{item.ProductName}</span>
                    </td>
                    <td style='padding: 12px; text-align: right;'>${item.Price:N2}</td>
                    <td style='padding: 12px; text-align: right; {priceChange}'>${item.UpdatedPrice:N2}</td>
                    <td style='padding: 12px; text-align: center;'>{item.Quantity}</td>
                    <td style='padding: 12px; text-align: center;'>{item.LeadTime} {(item.LeadTime == 1 ? "day" : "days")}</td>
                    <td style='padding: 12px; text-align: right;'>${itemTotal:N2}</td>
                    <td style='padding: 12px; text-align: center;'>
                        <img src='{imageUrl}'
                             style='max-width: 80px; max-height: 80px; border-radius: 4px;'
                             alt='{item.ProductName}'/>
                    </td>
                </tr>");
            }
        }

        // Show total price changes if both values exist
        if (originalTotalPrice.HasValue && updatedTotalPrice.HasValue)
        {
            var totalPriceChange = updatedTotalPrice > originalTotalPrice ? "color: #dc3545;" : "color: #28a745;";
            sb.Append($@"
            <tr style='background-color: #f8f9fa;'>
                <td colspan='5' style='padding: 12px; text-align: right;'>Original Total:</td>
                <td colspan='2' style='padding: 12px; text-align: right; color: #666;'>${originalTotalPrice:N2}</td>
            </tr>
            <tr style='background-color: #f8f9fa; font-weight: bold;'>
                <td colspan='5' style='padding: 12px; text-align: right;'>New Total Amount:</td>
                <td colspan='2' style='padding: 12px; text-align: right; {totalPriceChange}'>${updatedTotalPrice:N2}</td>
            </tr>");
        }

        sb.Append("</table>");
        return sb.ToString();
    }

    /// <summary>
    /// Adres bilgisini formatlar
    /// </summary>
    private string FormatAddress(UserAddressDto? address)
    {
        if (address == null) return "No address provided";

        var formattedAddress = new StringBuilder();
        formattedAddress.AppendLine(address.Name);
        formattedAddress.AppendLine(address.AddressLine1);

        if (!string.IsNullOrEmpty(address.AddressLine2))
            formattedAddress.AppendLine(address.AddressLine2);

        formattedAddress.AppendLine(
            $"{address.City}{(!string.IsNullOrEmpty(address.State) ? $", {address.State}" : "")} {address.PostalCode}");
        formattedAddress.Append(address.Country);

        return formattedAddress.ToString();
    }
}
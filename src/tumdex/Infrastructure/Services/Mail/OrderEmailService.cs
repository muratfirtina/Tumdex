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
/// Email service for price quote requests
/// </summary>
public class OrderEmailService : BaseEmailService, IOrderEmailService
{
    protected override string ServiceType => "QUOTE_REQUEST_EMAIL";
    protected override string ConfigPrefix => "Email:OrderEmail";
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
            var rateLimitKey = $"quote_request_email_ratelimit_{recipient}_{DateTime.UtcNow:yyyyMMddHH}";
            var count = await _cacheService.GetCounterAsync(rateLimitKey,cancellationToken: CancellationToken.None);

            if (count >= 10) // Higher limit for quote request emails
                throw new Exception($"Email rate limit exceeded for recipient: {recipient}");

            await _cacheService.IncrementAsync(rateLimitKey, 1, TimeSpan.FromHours(1),cancellationToken: CancellationToken.None);
        }
    }

    protected override string GetEmailTitleColor()
    {
        return "#059669"; // Green color for price quotes
    }

    protected override string GetFooterMessage()
    {
        return "This is an automated price quote request notification.<br>Please do not reply to this email.";
    }

    /// <summary>
    /// Sends a price quote request notification
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
            content.Append(BuildQuoteRequestContent(
                userName,
                orderCode,
                orderDescription,
                orderAddress,
                orderCreatedDate,
                orderCartItems,
                orderTotalPrice));

            var emailBody = await BuildEmailTemplate(content.ToString(), "Price Quote Request");
            await SendEmailAsync(to, "Your Price Quote Request ✓", emailBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send price quote request email");
            throw;
        }
    }

    /// <summary>
    /// Sends a price quote update notification
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
            content.Append(BuildQuoteUpdateContent(
                orderCode, adminNote, originalStatus, updatedStatus,
                originalTotalPrice, updatedTotalPrice, updatedItems));

            var emailBody = await BuildEmailTemplate(content.ToString(), "Price Quote Update Notification");
            await SendEmailAsync(to, "Price Quote Update Notification", emailBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send price quote update notification email");
            throw;
        }
    }

    /// <summary>
    /// Builds price quote request content
    /// </summary>
    private string BuildQuoteRequestContent(
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
            <p style='color: #666;'>Your price quote request has been successfully created.</p>
        </div>");

        // Quote request items table
        sb.Append(BuildQuoteItemsTable(orderCartItems));

        // Quote request information
        sb.Append($@"
        <div style='margin-top: 30px; padding: 20px; background-color: #f8f9fa; border-radius: 5px;'>
            <h3 style='color: #333333; margin-bottom: 15px;'>Price Quote Request Details</h3>
            <p style='color: #e53935;'><strong>Quote Reference: {orderCode}</strong></p>
            <p><strong>Request Date:</strong> {orderCreatedDate:dd.MM.yyyy HH:mm}</p>
            <p><strong>Delivery Address:</strong><br>{FormatAddress(orderAddress)}</p>
            <p><strong>Request Notes:</strong><br>{orderDescription}</p>
            <p style='color: #059669;'><strong>Total Estimate:</strong><br>We will prepare a custom price quote for your requested items and share detailed pricing information with you shortly.</p>
        </div>");

        return sb.ToString();
    }

    /// <summary>
    /// Builds price quote items table
    /// </summary>
    private string BuildQuoteItemsTable(List<OrderItemDto> items)
    {
        var sb = new StringBuilder();

        sb.Append(@"
        <table style='width: 100%; border-collapse: collapse; margin-top: 10px;'>
            <tr style='background-color: #333333; color: white;'>
                <th style='padding: 12px; text-align: left;'>Product</th>
                <th style='padding: 12px; text-align: right;'>Unit Price</th>
                <th style='padding: 12px; text-align: center;'>Quantity</th>
                <th style='padding: 12px; text-align: right;'>Sub Total</th>
                <th style='padding: 12px; text-align: center;'>Image</th>
            </tr>");

        foreach (var item in items)
        {
            var itemTotal = (item.Price ?? 0) * (item.Quantity ?? 0);
            string imageUrl = item.ShowcaseImage?.Url ?? "";

            sb.Append($@"
            <tr style='border-bottom: 1px solid #e0e0e0;'>
                <td style='padding: 12px;'>
                    <strong style='color: #333;'>{item.BrandName}</strong><br>
                    <span style='color: #666;'>{item.ProductName}</span>
                </td>
                <td style='padding: 12px; text-align: right;'>Requested</td>
                <td style='padding: 12px; text-align: center;'>{item.Quantity}</td>
                <td style='padding: 12px; text-align: right;'> - </td>
                <td style='padding: 12px; text-align: center;'>
                    <img src='{imageUrl}' style='max-width: 80px; max-height: 80px; border-radius: 4px;'
                         alt='{item.ProductName}'/>
                </td>
            </tr>");
        }

        sb.Append($@"
        <tr style='background-color: #f8f9fa; font-weight: bold;'>
            <td colspan='3' style='padding: 12px; color: #232323 text-align: right;'>Quote Status:</td>
            <td colspan='2' style='padding: 12px; color: #FFA942 text-align: right;'>Pending</td>
        </tr>");

        sb.Append("</table>");
        return sb.ToString();
    }

    /// <summary>
    /// Builds price quote update content
    /// </summary>
    private string BuildQuoteUpdateContent(
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
                <p style='color: #666;'>We would like to inform you about updates to your price quote request.</p>
            </div>

            <div style='background-color: #fff3cd; padding: 20px; border-radius: 5px; margin-bottom: 20px;'>
                <p style='color: #e53935;'><strong>Quote Reference: {orderCode}</strong></p>");

        // Show status change if both statuses exist and are different
        if (originalStatus.HasValue && updatedStatus.HasValue && originalStatus != updatedStatus)
        {
            sb.Append($@"
                <p style='color: #856404;'>
                    <strong>Quote Status:</strong> Changed from <span style='color: #6c757d;'>{GetStatusText(originalStatus)}</span> 
                    to <span style='color: #28a745;'>{GetStatusText(updatedStatus)}</span>
                </p>");
        }

        // Admin note if exists
        if (!string.IsNullOrEmpty(adminNote))
        {
            sb.Append($@"<p style='color: #856404;'><strong>Note from our team:</strong> {adminNote}</p>");
        }

        sb.Append("</div>");

        // Updated items table if exists
        if (updatedItems?.Any() == true)
        {
            sb.Append(BuildUpdatedQuoteItemsTable(updatedItems, originalTotalPrice, updatedTotalPrice));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Converts status to user-friendly text
    /// </summary>
    private string GetStatusText(OrderStatus? status)
    {
        return status switch
        {
            OrderStatus.Pending => "Quote Pending",
            OrderStatus.Processing => "Quote in Progress",
            OrderStatus.Shipped => "Quote Ready",
            OrderStatus.Delivered => "Quote Completed",
            OrderStatus.Cancelled => "Quote Cancelled",
            _ => status.ToString() ?? "Unknown"
        };
    }

    /// <summary>
    /// Builds updated quote items table
    /// </summary>
    private string BuildUpdatedQuoteItemsTable(
        List<OrderItemUpdateDto> items,
        decimal? originalTotalPrice,
        decimal? updatedTotalPrice)
    {
        var sb = new StringBuilder();

        sb.Append(@"
        <table style='width: 100%; border-collapse: collapse; margin-top: 20px;'>
            <tr style='background-color: #333333; color: white;'>
                <th style='padding: 12px; text-align: left;'>Product</th>
                <th style='padding: 12px; text-align: right;'>Unit Price</th>
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
                string imageUrl = item.ShowcaseImage?.Url ?? string.Empty;

                sb.Append($@"
                <tr style='border-bottom: 1px solid #e0e0e0;'>
                    <td style='padding: 12px;'>
                        <strong style='color: #333;'>{item.BrandName}</strong><br>
                        <span style='color: #666;'>{item.ProductName}</span>
                    </td>
                    <td style='padding: 12px; text-align: right; color: #3B82F6;'>${item.UpdatedPrice:N2}</td>
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
            sb.Append($@"
            <tr style='background-color: #f8f9fa; font-weight: bold;'>
                <td colspan='5' style='padding: 12px; text-align: right;'> Total Amount:</td>
                <td colspan='2' style='padding: 12px; text-align: right; color: #059669;'>${updatedTotalPrice:N2}</td>
            </tr>");
        }

        sb.Append("</table>");
        return sb.ToString();
    }

    /// <summary>
    /// Formats address information
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
            $"{address.CityName}{(!string.IsNullOrEmpty(address.DistrictName) ? $", {address.DistrictName}" : "")} {address.PostalCode}");
        formattedAddress.Append(address.CountryName);

        return formattedAddress.ToString();
    }
}
using System.Text.Json.Serialization;

namespace Domain.Enum;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderStatus
{
    Pending,    // Sipariş beklemede
    Processing, // Sipariş hazırlanıyor
    Confirmed,  // Sipariş onaylandı
    Rejected,   // Sipariş reddedildi
    Delivered,  // Sipariş teslim edildi
    Completed,  // Sipariş tamamlandı
    Shipped,    // Sipariş gönderildi
    Cancelled,  // Sipariş iptal edildi
    Returned,    // Sipariş iade edildi
    All         // Tüm siparişler
}
using Application.Enums;

namespace Application.Models.Monitoring;

public class MetricAlert
{
    /// <summary>
    /// Alert'in benzersiz adı
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Alert türü
    /// </summary>
    public AlertType Type { get; set; }

    /// <summary>
    /// Alert mesajı
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Alert ile ilgili etiketler ve değerleri
    /// </summary>
    public Dictionary<string, string> Labels { get; set; }

    /// <summary>
    /// Alert'in oluşturulma zamanı
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Metrik değeri (varsa)
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// Eşik değeri (varsa)
    /// </summary>
    public string Threshold { get; set; }

    /// <summary>
    /// Alert seviyesi (info, warning, error, critical)
    /// </summary>
    public string Severity { get; set; }

    /// <summary>
    /// Alert'in kaynağı (service name, component, etc.)
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Alert ile ilgili detaylı bilgi
    /// </summary>
    public string Details { get; set; }
}

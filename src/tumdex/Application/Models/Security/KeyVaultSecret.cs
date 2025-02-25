namespace Application.Models.Security;

public class KeyVaultSecret
{
    /// <summary>
    /// Secret'ın adı
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Secret'ın değeri
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Secret'ın oluşturulma tarihi
    /// </summary>
    public DateTime CreatedOn { get; set; }

    /// <summary>
    /// Secret'ın son güncelleme tarihi
    /// </summary>
    public DateTime? UpdatedOn { get; set; }

    /// <summary>
    /// Secret'ın etiketi (opsiyonel)
    /// </summary>
    public string Tags { get; set; }

    /// <summary>
    /// Secret'ın son kullanma tarihi (opsiyonel)
    /// </summary>
    public DateTime? ExpiresOn { get; set; }
}
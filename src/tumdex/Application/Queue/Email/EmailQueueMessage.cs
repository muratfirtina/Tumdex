namespace Application.Queue.Email;

public class EmailQueueMessage
{
    public string To { get; set; }
    public string Subject { get; set; }
    public string Body { get; set; }
    public EmailType Type { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

    // Başarısız e-posta gönderim denemelerini takip etmek için
    public int RetryCount { get; set; } = 0;
    public DateTime? LastRetryTime { get; set; } = null;
}
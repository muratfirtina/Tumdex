namespace Infrastructure.Services.Security.Models;

public class EmailSettings
{
    public string? SmtpServer { get; set; }
    public int Port { get; set; }
    public string? FromAddress { get; set; }
    public string? ToAddress { get; set; }
    public string? FromName { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool EnableSsl { get; set; }
    public bool UseGraphApi { get; set; }
}
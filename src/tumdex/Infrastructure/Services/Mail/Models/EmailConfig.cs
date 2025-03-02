namespace Infrastructure.Services.Mail.Models;

public class EmailConfig
{
    public string FromName { get; set; }
    public string FromAddress { get; set; }
    public string ToAddress { get; set; }
    public string Server { get; set; }
    public int Port { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public bool UseSsl { get; set; }
    public bool RequireTls { get; set; }
    public bool AllowInvalidCert { get; set; }
}
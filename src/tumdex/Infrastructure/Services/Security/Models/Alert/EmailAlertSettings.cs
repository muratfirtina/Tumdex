namespace Infrastructure.Services.Security.Models.Alert;

public class EmailAlertSettings
{
    public bool Enabled { get; set; }
    public List<string> Recipients { get; set; }
}
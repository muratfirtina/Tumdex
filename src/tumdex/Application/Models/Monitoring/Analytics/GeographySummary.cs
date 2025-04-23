namespace Application.Models.Monitoring.Analytics;

public class GeographySummary
{
    public string Country { get; set; }
    public string City { get; set; }
    public int VisitCount { get; set; }
    
    public GeographySummary()
    {
        Country = string.Empty;
        City = string.Empty;
        VisitCount = 0;
    }
}
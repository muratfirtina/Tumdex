namespace Application.Features.Dashboard.Dtos;

public class DashboardStatisticsDto
{
    public int UserCount { get; set; }
    public int ProductCount { get; set; }
    public int OrderCount { get; set; }
    public decimal? TotalRevenue { get; set; }
    public int CategoryCount { get; set; }
    public int BrandCount { get; set; }
    public string TimeFrame { get; set; }
}
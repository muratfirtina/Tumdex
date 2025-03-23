namespace Application.Features.Dashboard.Dtos;

public class TopLocationDto
{
    public string CountryName { get; set; }
    public string CityName { get; set; }
    public int Count { get; set; }
    public double Percentage { get; set; }
}
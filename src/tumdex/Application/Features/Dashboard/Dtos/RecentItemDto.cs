using Application.Dtos.Image;

namespace Application.Features.Dashboard.Dtos;

public class RecentItemDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public DateTime? CreatedDate { get; set; }
    public ImageFileDto Image { get; set; }
}
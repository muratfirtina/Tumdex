using System.ComponentModel.DataAnnotations.Schema;
using Core.Persistence.Repositories;

namespace Domain;

public class Carousel : Entity<string>
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int Order { get; set; }
    public bool IsActive { get; set; }
    public ICollection<CarouselImageFile> CarouselImageFiles { get; set; }

    [NotMapped]
    public string Url { get; set; }
    
    public Carousel(string? name): base(name)
    {
        Name = name;
    }
}
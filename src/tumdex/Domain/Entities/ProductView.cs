using Core.Persistence.Repositories;
using Domain.Identity;

namespace Domain;

public class ProductView : Entity<string>
{
    public string ProductId { get; set; }
    public Product Product { get; set; }
    public string UserId { get; set; }
    public AppUser User { get; set; }
    public DateTime VisitDate { get; set; }
    
    public ProductView() : base("ProductView")
    {
        
    }
}
using Microsoft.AspNetCore.Identity;

namespace Domain.Identity;

public class AppRole : IdentityRole<string>
{
    public ICollection<Endpoint> Endpoints { get; set; }
    
}
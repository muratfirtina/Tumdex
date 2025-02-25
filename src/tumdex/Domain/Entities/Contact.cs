using Core.Persistence.Repositories;

namespace Domain;

public class Contact:Entity<string>
{
    public string Name { get; set; }
    public string Email { get; set; }
    public string Subject { get; set; }
    public string Message { get; set; }
    public DateTime CreatedDate { get; set; }
    public bool IsRead { get; set; }
    
    public Contact()
    {
        
    }

    public Contact(string name, string email, string subject, string message) : base(name)
    {
        Name = name;
        Email = email;
        Subject = subject;
        Message = message;
        IsRead = false;
    }
}
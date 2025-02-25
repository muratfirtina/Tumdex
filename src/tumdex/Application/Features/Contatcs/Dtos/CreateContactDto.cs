namespace Application.Features.Contatcs.Dtos;

public class CreateContactDto
{
    public string Name { get; set; }
    public string Email { get; set; }
    public string Subject { get; set; }
    public string Message { get; set; }
}
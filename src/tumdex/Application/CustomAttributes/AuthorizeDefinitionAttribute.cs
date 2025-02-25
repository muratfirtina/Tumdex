using Application.Enums;

namespace Application.CustomAttributes;

public class AuthorizeDefinitionAttribute: Attribute
{
    public string Menu { get; set; }//which controller
    public string Definition { get; set; }
    public ActionType ActionType { get; set; }
}
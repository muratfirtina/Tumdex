using Application.Abstraction.Services.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WebAPI.Attributes;

public class ValidateAntiForgeryTokenAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var antiForgeryService = context.HttpContext.RequestServices.GetRequiredService<IAntiForgeryService>();
        
        if (!antiForgeryService.ValidateToken(context.HttpContext))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }
        
        base.OnActionExecuting(context);
    }
}
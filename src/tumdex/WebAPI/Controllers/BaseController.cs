using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    public class BaseController : ControllerBase
    {
        protected IMediator Mediator =>
            _mediator ??=
                HttpContext.RequestServices.GetService<IMediator>()
                ?? throw new InvalidOperationException("IMediator cannot be retrieved from request services.");

        private IMediator? _mediator;

        protected string getIpAddress()
        {
            if (Request.Headers.ContainsKey("X-Forwarded-For"))
            {
                return Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim();
            }

            return HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString()
                   ?? throw new InvalidOperationException("IP address cannot be retrieved from request.");
        }

        /*protected Guid getUserIdFromRequest() //todo authentication behavior?
        {
            var userId = HttpContext.User.GetUserId();
            return userId;
        }*/
    }
}

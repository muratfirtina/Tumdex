using Application.Consts;
using Application.CustomAttributes;
using Application.Enums;
using Application.Features.Newsletters.Commands.SendMonthly;
using Application.Features.Newsletters.Commands.Subscribe;
using Application.Features.Newsletters.Commands.Unsubscribe;
using Application.Features.Newsletters.Queries.GetList;
using Core.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NewsletterController : BaseController
    {
        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] SubscribeCommand subscribeCommand)
        {
            var result = await Mediator.Send(subscribeCommand);
            return Ok(result);
        }
        
        [HttpPost("unsubscribe")]
        public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeCommand unsubscribeCommand)
        {
            var result = await Mediator.Send(unsubscribeCommand);
            return Ok(result);
        }
        [HttpGet]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get List Newsletter", Menu = AuthorizeDefinitionConstants.Newsletters)]
        public async Task<IActionResult> GetList([FromQuery] PageRequest pageRequest)
        {
            GetListNewsletterQuery getListNewsletterQuery = new() { PageRequest = pageRequest };
            var result = await Mediator.Send(getListNewsletterQuery);
            return Ok(result);
        }

        [HttpPost("send-monthly-newsletter")]
        public async Task<IActionResult> SendMonthlyNewsletter([FromQuery] bool isTest = false)
        {
            SendMonthlyNewsletterCommand command = new() { IsTest = isTest };
            await Mediator.Send(command);
            return Ok(new { message = $"{(isTest ? "Test" : "Monthly")} newsletter sent successfully" });
        }
        
    }
}

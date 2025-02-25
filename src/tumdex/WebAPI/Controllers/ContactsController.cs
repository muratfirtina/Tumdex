using Application.Features.Contatcs.Command;
using Application.Services;
using Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContactsController : BaseController
    {
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateContactCommand createContactCommand)
        {
            CreatedContactResponse response = await Mediator.Send(createContactCommand);
            return Ok(response);
        }
    }
}

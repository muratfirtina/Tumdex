using System;
   using System.Security.Claims;
   using System.Threading.Tasks;
   using Application.Consts;
   using Application.CustomAttributes;
   using Application.Enums;
   using Application.Exceptions;
   using Application.Features.Users.Commands.LoginUser;
   using Application.Features.Users.Commands.LogoutUser;
   using Application.Features.Users.Commands.PasswordReset;
   using Microsoft.AspNetCore.Authorization;
   using Microsoft.AspNetCore.Mvc;
   
   namespace WebAPI.Controllers
   {
       [Route("api/auth")]
       [ApiController]
       public class AuthController : BaseController
       {
           private readonly ILogger<AuthController> _logger;
   
           public AuthController(ILogger<AuthController> logger)
           {
               _logger = logger;
           }
   
           [HttpPost("login")]
           [ProducesResponseType(StatusCodes.Status200OK)]
           [ProducesResponseType(StatusCodes.Status401Unauthorized)]
           [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
           public async Task<IActionResult> Login([FromBody] LoginUserRequest request)
           {
               try
               {
                   // Set client information
                   request.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                   request.UserAgent = Request.Headers["User-Agent"].ToString();
   
                   // Forward-For header takes precedence if available
                   if (Request.Headers.ContainsKey("X-Forwarded-For"))
                   {
                       request.IpAddress = Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim();
                   }
   
                   // Process the request through MediatR
                   var result = await Mediator.Send(request);
                   return Ok(result);
               }
               catch (AuthenticationErrorException ex)
               {
                   _logger.LogWarning("Authentication error: {Message}", ex.Message);
   
                   if (ex.Message.Contains("kilitlendi"))
                   {
                       // This is an account lockout
                       return StatusCode(StatusCodes.Status401Unauthorized, 
                           new { error = ex.Message });
                   }
                   else if (ex.Message.Contains("Çok fazla giriş denemesi"))
                   {
                       // This is a rate limit
                       return StatusCode(StatusCodes.Status429TooManyRequests, 
                           new { error = ex.Message, retryAfter = 300 });
                   }
                   else
                   {
                       // Standard authentication error
                       return StatusCode(StatusCodes.Status401Unauthorized, 
                           new { error = "Invalid username/email or password" });
                   }
               }
               catch (ArgumentException ex)
               {
                   // Validation error
                   return BadRequest(new { error = ex.Message });
               }
               catch (Exception ex)
               {
                   _logger.LogError(ex, "Unexpected error during login");
                   return StatusCode(StatusCodes.Status500InternalServerError, 
                       new { error = "An error occurred. Please try again later." });
               }
           }
   
           [HttpPost("logout")]
           [Authorize(AuthenticationSchemes = "Admin")]
           [ProducesResponseType(StatusCodes.Status204NoContent)]
           [ProducesResponseType(StatusCodes.Status400BadRequest)]
           [ProducesResponseType(StatusCodes.Status500InternalServerError)]
           [AuthorizeDefinition(Menu = AuthorizeDefinitionConstants.Security, Definition = "User Logout", 
               ActionType = ActionType.Writing)]
           public async Task<IActionResult> Logout([FromBody] LogoutUserCommand request)
           {
               if (string.IsNullOrEmpty(request.RefreshToken))
               {
                   return BadRequest(new { error = "Refresh token is required" });
               }
   
               try
               {
                   request.IpAddress = getIpAddress();
                   await Mediator.Send(request);
                   return NoContent();
               }
               catch (Exception ex)
               {
                   _logger.LogError(ex, "Logout error");
                   return StatusCode(StatusCodes.Status500InternalServerError, 
                       new { error = "An error occurred while logging out" });
               }
           }
   
           [HttpPost("password-reset")]
           [ProducesResponseType(StatusCodes.Status200OK)]
           public async Task<IActionResult> PasswordReset([FromBody] PasswordResetRequest request)
           {
               var response = await Mediator.Send(request);
               return Ok(response);
           }
       }
   }
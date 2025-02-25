using System.Diagnostics;
using Application.Abstraction.Services;
using Application.Features.Users.Commands.LoginUser;
using Application.Features.Users.Commands.LogoutUser;
using Application.Features.Users.Commands.PasswordReset;
using Application.Features.Users.Commands.RefreshTokenLogin;
using Application.Features.Users.Commands.VerifyResetPasswordToken;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : BaseController
    {
        private readonly IMetricsService _metricsService;

        public AuthController(IMetricsService metricsService)
        {
            _metricsService = metricsService;
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> Login(LoginUserRequest loginUserRequest)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var response = await Mediator.Send(loginUserRequest);
                stopwatch.Stop();

                // Başarılı login metriği
                _metricsService.IncrementUserLogins("jwt", "standard");
                _metricsService.UpdateActiveUsers("authenticated", 1);
                _metricsService.RecordRequestDuration(
                    "auth",
                    "login",
                    stopwatch.Elapsed.TotalMilliseconds);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _metricsService.IncrementFailedLogins(
                    ex.GetType().Name,
                    "standard");
                throw;
            }
        }
        
        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var response = await Mediator.Send(new LogoutUserCommand());
                if (response)
                {
                    return Ok(new { message = "Logged out successfully" });
                }
                return BadRequest(new { message = "Logout failed" });
            }
            catch
            {
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { message = "An error occurred during logout" });
            }
        }
        
        
        [HttpPost("[action]")]
        public async Task<IActionResult> RefreshTokenLogin([FromBody]RefreshTokenLoginRequest refreshTokenLoginRequest)
        {
            var response = await Mediator.Send(refreshTokenLoginRequest);
            return Ok(response);
        }
        
        [HttpPost("password-reset")]
        public async Task<IActionResult> PasswordReset(PasswordResetRequest passwordResetRequest)
        {
            var response = await Mediator.Send(passwordResetRequest);
            return Ok(response);
        }
        
        [HttpPost("verify-reset-password-token")]
        public async Task<IActionResult> VerifyResetPasswordToken([FromBody]VerifyResetPasswordTokenRequest verifyResetPasswordTokenRequest)
        {
            var response = await Mediator.Send(verifyResetPasswordTokenRequest);
            return Ok(response);
        }
    }
}

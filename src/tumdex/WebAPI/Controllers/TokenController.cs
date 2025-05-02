using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Application.Abstraction.Services.Utilities;
using Application.Consts;
using Application.CustomAttributes;
using Application.Enums;
using Application.Features.Tokens.Command.ActivationCode.ResendActivationCode;
using Application.Features.Tokens.Command.ActivationCode.VerifyActivationCode;
using Application.Features.Tokens.Command.RefreshTokenLogin;
using Application.Features.Tokens.Command.RevokeUserTokens;
using Application.Features.Tokens.Command.ValidateToken;
using Application.Features.Tokens.Command.VerifyActivationToken;
using Application.Features.Tokens.Command.VerifyResetPasswordToken;
using Application.Features.Users.Commands.LogoutAllDevices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace WebAPI.Controllers
{
    [Route("api/token")]
    [ApiController]
    public class TokenController : BaseController
    {
        private readonly ILogger<TokenController> _logger;

        public TokenController(ILogger<TokenController> logger)
        {
            _logger = logger;
        }

        [HttpPost("refresh")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [AuthorizeDefinition(Menu = AuthorizeDefinitionConstants.Security, Definition = "Refresh Token", 
            ActionType = ActionType.Reading)]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenLoginRequest request)
        {
            if (string.IsNullOrEmpty(request.RefreshToken))
            {
                return BadRequest(new { error = "Refresh token is required" });
            }

            try
            {
                request.IpAddress = getIpAddress();
                request.UserAgent = Request.Headers["User-Agent"].ToString() ?? "Unknown";

                var result = await Mediator.Send(request);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Refresh token validation error");
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token refresh error");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "An error occurred while refreshing the token" });
            }
        }

        [HttpPost("logout-all")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [AuthorizeDefinition(Menu = AuthorizeDefinitionConstants.Security, Definition = "Logout From All Devices", 
            ActionType = ActionType.Deleting)]
        public async Task<IActionResult> LogoutAllDevices()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest(new { error = "User ID not found" });
                }

                var command = new LogoutAllDevicesCommand
                {
                    UserId = userId,
                    IpAddress = getIpAddress(),
                    Reason = "Logout from all devices"
                };

                await Mediator.Send(command);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout from all devices error");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "An error occurred while logging out from all devices" });
            }
        }

        [HttpGet("validate")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ValidateToken()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { isValid = false, error = "User ID not found" });
                }

                // Create a command for token validation
                var command = new ValidateTokenCommand
                {
                    UserId = userId,
                    Token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "")
                };

                var result = await Mediator.Send(command);
                
                if (result.IsValid)
                {
                    return Ok(new { isValid = true });
                }
                else
                {
                    return Unauthorized(new { isValid = false, error = result.Error });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token validation error");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { isValid = false, error = "An error occurred during token validation" });
            }
        }

        [HttpPost("admin/users/{userId}/revoke")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [AuthorizeDefinition(Menu = AuthorizeDefinitionConstants.UserManagement, Definition = "Revoke User Tokens", 
            ActionType = ActionType.Deleting)]
        public async Task<IActionResult> RevokeUserTokens(string userId)
        {
            try
            {
                var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var command = new RevokeUserTokensCommand
                {
                    UserId = userId,
                    AdminId = adminId,
                    IpAddress = getIpAddress()
                };

                await Mediator.Send(command);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin token revocation error");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "An error occurred while revoking user tokens" });
            }
        }

        [HttpPost("verify-reset-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> VerifyResetPasswordToken([FromBody] VerifyResetPasswordTokenRequest request)
        {
            var response = await Mediator.Send(request);
            return Ok(response);
        }

        [HttpPost("activation-code/verify")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> VerifyActivationCode([FromBody] VerifyActivationCodeCommand command)
        {
            _logger.LogInformation("Activation code verification request: UserId={UserId}, CodeLength={CodeLength}", 
                command.UserId, command.Code?.Length ?? 0);

            if (string.IsNullOrEmpty(command.UserId) || string.IsNullOrEmpty(command.Code))
            {
                _logger.LogWarning(
                    "Activation request with missing parameters: UserId={UserId}, CodeProvided={CodeProvided}", 
                    command.UserId ?? "null", !string.IsNullOrEmpty(command.Code));
                return BadRequest(new { message = "UserId and activation code are required." });
            }

            // Get ICacheService for rate limit checks
            var cacheService = HttpContext.RequestServices.GetRequiredService<ICacheService>();
            
            // Attempt count check
            string rateLimitKey = $"activation_attempts_{command.UserId}";
            var attemptCount = await cacheService.GetCounterAsync(rateLimitKey,cancellationToken: CancellationToken.None);
            
            // Block if exceeded 5 attempts
            if (attemptCount >= 5)
            {
                // Handle rate limiting in controller - this is application-level validation
                await cacheService.RemoveAsync($"email_activation_code_{command.UserId}", cancellationToken: CancellationToken.None);
                await cacheService.RemoveAsync($"activation_token_{command.UserId}",cancellationToken: CancellationToken.None);

                return StatusCode(StatusCodes.Status429TooManyRequests, new
                {
                    message = "Too many failed attempts. Your activation code has been invalidated. Please request a new activation code.",
                    exceeded = true,
                    requiresNewCode = true
                });
            }

            // Process the request through MediatR
            var result = await Mediator.Send(command);

            if (result.Verified)
            {
                // Clear cache data on success
                await cacheService.RemoveAsync(rateLimitKey,cancellationToken: CancellationToken.None);
                await cacheService.RemoveAsync($"email_activation_code_{command.UserId}",cancellationToken: CancellationToken.None);
                await cacheService.RemoveAsync($"activation_token_{command.UserId}",cancellationToken: CancellationToken.None);
                await cacheService.SetAsync($"activation_completed_{command.UserId}", true, TimeSpan.FromDays(90),cancellationToken: CancellationToken.None);

                _logger.LogInformation("Activation completed for user {UserId} and URL/code destroyed", command.UserId);

                return Ok(new
                {
                    message = "Your email has been successfully verified.",
                    verified = true
                });
            }
            else
            {
                // Increment counter on failed attempt (for 30 minutes)
                await cacheService.IncrementAsync(rateLimitKey, 1, TimeSpan.FromMinutes(30),cancellationToken: CancellationToken.None);

                // Get updated counter
                var updatedCount = await cacheService.GetCounterAsync(rateLimitKey,cancellationToken: CancellationToken.None);
                var remainingAttempts = Math.Max(0, 5 - updatedCount);

                return BadRequest(new
                {
                    message = $"Invalid activation code. You have {remainingAttempts} attempts remaining.",
                    verified = false,
                    remainingAttempts = remainingAttempts
                });
            }
        }

        [HttpPost("activation-code/resend")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> ResendActivationCode([FromBody] ResendActivationCodeCommand command)
        {
            if (string.IsNullOrEmpty(command.Email))
            {
                return BadRequest(new { message = "Email address is required.", success = false });
            }

            try
            {
                // Rate limiting for activation code resends
                var cacheService = HttpContext.RequestServices.GetRequiredService<ICacheService>();
                string rateLimitKey = $"activation_resend_{command.Email.ToLower()}";
                
                // Maximum 3 resend requests (within 1 hour)
                var resendCount = await cacheService.GetCounterAsync(rateLimitKey,cancellationToken: CancellationToken.None);
                if (resendCount >= 3)
                {
                    return StatusCode(StatusCodes.Status429TooManyRequests, new
                    {
                        message = "Too many code requests. Please try again after 1 hour.",
                        success = false
                    });
                }

                // Process the command through MediatR
                var response = await Mediator.Send(command);
                
                // Increment rate limiter
                await cacheService.IncrementAsync(rateLimitKey, 1, TimeSpan.FromHours(1),cancellationToken: CancellationToken.None);
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending activation code to {Email}", command.Email);
                
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { 
                        message = "An error occurred while sending the activation code. Please try again later.", 
                        success = false 
                    });
            }
        }

        [HttpGet("activation/verify")]
        public async Task<IActionResult> VerifyActivationToken([FromQuery] string token)
        {
            _logger.LogInformation("Token verification request received: {TokenLength} characters", token?.Length ?? 0);
            if (string.IsNullOrEmpty(token))
            {
                return BadRequest(new { success = false, message = "Token is required" });
            }

            try
            {
                // Apply rate limiting
                var cacheService = HttpContext.RequestServices.GetRequiredService<ICacheService>();
                string ipAddress = getIpAddress();
                string rateLimitKey = $"token_verify_{ipAddress}";
                
                // Accept at most 10 requests per minute per IP
                int requestCount = await cacheService.GetCounterAsync(rateLimitKey,cancellationToken: CancellationToken.None);
                if (requestCount >= 10)
                {
                    return StatusCode(StatusCodes.Status429TooManyRequests, new
                    {
                        success = false,
                        message = "Too many token verification requests sent. Please try again after a while."
                    });
                }

                // Increment request counter (for 1 minute)
                await cacheService.IncrementAsync(rateLimitKey, 1, TimeSpan.FromMinutes(1),cancellationToken: CancellationToken.None);

                // Create command for token verification
                var command = new VerifyActivationTokenCommand { Token = token };
                var result = await Mediator.Send(command);
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "General token verification error");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { success = false, message = "An error occurred while processing the token" });
            }
        }
    }
}
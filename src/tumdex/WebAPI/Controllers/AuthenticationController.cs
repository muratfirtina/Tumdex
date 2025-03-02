using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Application.Abstraction.Services;
using Application.Consts;
using Application.CustomAttributes;
using Application.Enums;
using Application.Features.Users.Commands.LoginUser;
using Application.Features.Users.Commands.LogoutAllDevices;
using Application.Features.Users.Commands.LogoutUser;
using Application.Features.Users.Commands.RefreshTokenLogin;
using Application.Features.Users.Commands.RevokeUserTokens;
using Application.Features.Users.Commands.ConfirmEmail;
using Application.Features.Users.Commands.ResendConfirmationEmail;
using Application.Features.Users.Commands.PasswordReset;
using Application.Features.Users.Commands.VerifyResetPasswordToken;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace WebAPI.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthenticationController : BaseController
    {
        private readonly ILogger<AuthenticationController> _logger;
        private readonly IMetricsService _metricsService;

        public AuthenticationController(
            ILogger<AuthenticationController> logger,
            IMetricsService metricsService)
        {
            _logger = logger;
            _metricsService = metricsService;
        }

        #region Authentication Operations

        [HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [AuthorizeDefinition(Menu = AuthorizeDefinitionConstants.Security, Definition = "User Login", ActionType = ActionType.Reading)]
        public async Task<IActionResult> Login([FromBody] LoginUserRequest request)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try 
            {
                // Set client information
                request.IpAddress = getIpAddress();
                request.UserAgent = Request.Headers["User-Agent"].ToString() ?? "Unknown";
                
                var result = await Mediator.Send(request);
                stopwatch.Stop();

                try
                {
                    // Track metrics for successful login
                    _metricsService?.IncrementUserLogins("jwt", "standard");
                    _metricsService?.UpdateActiveUsers("authenticated", 1);
                    _metricsService?.RecordRequestDuration(
                        "auth",
                        "login",
                        stopwatch.Elapsed.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    // Suppress metrics errors
                    _logger.LogDebug($"Metrics error: {ex.Message}");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                try
                {
                    _metricsService?.IncrementFailedLogins(ex.GetType().Name, "standard");
                }
                catch
                {
                    // Suppress metrics errors
                }

                _logger.LogError(ex, "Login failed");
                return StatusCode(StatusCodes.Status401Unauthorized, 
                    new { error = "Invalid username or password" });
            }
        }

        [HttpPost("refresh")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [AuthorizeDefinition(Menu = AuthorizeDefinitionConstants.Security, Definition = "Refresh Token", ActionType = ActionType.Reading)]
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

        [HttpPost("logout")]
        [Authorize(AuthenticationSchemes = "User")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [AuthorizeDefinition(Menu = AuthorizeDefinitionConstants.Security, Definition = "User Logout", ActionType = ActionType.Writing)]
        public async Task<IActionResult> Logout([FromBody] LogoutUserCommand request)
        {
            if (string.IsNullOrEmpty(request.RefreshToken))
            {
                return BadRequest(new { error = "Refresh token is required" });
            }
            
            try
            {
                request.IpAddress = getIpAddress();
                
                var result = await Mediator.Send(request);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout error");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "An error occurred while logging out" });
            }
        }

        #endregion

        #region Advanced Token Management

        [HttpPost("logout-all")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [AuthorizeDefinition(Menu = AuthorizeDefinitionConstants.Security, Definition = "Logout From All Devices", ActionType = ActionType.Deleting)]
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

        [HttpPost("admin/users/{userId}/revoke-tokens")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [AuthorizeDefinition(Menu = AuthorizeDefinitionConstants.UserManagement, Definition = "Revoke User Tokens", ActionType = ActionType.Deleting)]
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

        #endregion

        #region Account Management

        [HttpGet("confirm-email")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [AuthorizeDefinition(Menu = AuthorizeDefinitionConstants.Users, Definition = "Confirm Email", ActionType = ActionType.Writing)]
        public async Task<IActionResult> ConfirmEmail([FromQuery] string userId, [FromQuery] string token)
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                var command = new ConfirmEmailCommand { UserId = userId, Token = token };
                var result = await Mediator.Send(command);
                
                stopwatch.Stop();
                
                _metricsService.RecordRequestDuration(
                    "auth",
                    "confirm_email",
                    stopwatch.Elapsed.TotalMilliseconds);
                
                if (result.Succeeded)
                {
                    _metricsService.IncrementCounter("email_confirmations", "success");
                    return Ok(new { message = "Email confirmed successfully. You can now login." });
                }
                
                _metricsService.IncrementCounter("email_confirmations", "failed");
                return BadRequest(new { 
                    message = "Email confirmation failed", 
                    errors = result.Errors.Select(e => e.Description) 
                });
            }
            catch (Exception ex)
            {
                _metricsService.RecordSecurityEvent("email_confirmation_error", "error");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { message = "An error occurred during email confirmation", error = ex.Message });
            }
        }
        
        [HttpPost("resend-confirmation-email")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [AuthorizeDefinition(Menu = AuthorizeDefinitionConstants.Users, Definition = "Resend Confirmation Email", ActionType = ActionType.Writing)]
        public async Task<IActionResult> ResendConfirmationEmail([FromBody] ResendConfirmationEmailCommand command)
        {
            try
            {
                // Rate limiting check would go here
                // For now we'll assume it's handled by middleware or a service
                
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                await Mediator.Send(command);
                
                stopwatch.Stop();
                
                try
                {
                    _metricsService?.RecordRequestDuration(
                        "auth",
                        "resend_confirmation",
                        stopwatch.Elapsed.TotalMilliseconds);
                    
                    _metricsService?.IncrementCounter("email_confirmation_resend", "success");
                }
                catch (Exception metricEx)
                {
                    _logger.LogDebug($"Metrics error: {metricEx.Message}");
                }
                
                // Always return the same response for security
                return Ok(new { message = "If your email exists in our system, a confirmation link has been sent. Please check your email." });
            }
            catch (Exception ex)
            {
                try
                {
                    _metricsService?.RecordSecurityEvent("email_confirmation_resend_error", "error");
                }
                catch
                {
                    // Ignore metrics errors
                }
                
                // Don't expose error details to the user
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { message = "An error occurred. Please try again later." });
            }
        }

        [HttpPost("password-reset")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [AuthorizeDefinition(Menu = AuthorizeDefinitionConstants.Security, Definition = "Request Password Reset", ActionType = ActionType.Writing)]
        public async Task<IActionResult> PasswordReset([FromBody] PasswordResetRequest request)
        {
            var response = await Mediator.Send(request);
            return Ok(response);
        }
        
        [HttpPost("verify-reset-password-token")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [AuthorizeDefinition(Menu = AuthorizeDefinitionConstants.Security, Definition = "Verify Password Reset Token", ActionType = ActionType.Reading)]
        public async Task<IActionResult> VerifyResetPasswordToken([FromBody] VerifyResetPasswordTokenRequest request)
        {
            var response = await Mediator.Send(request);
            return Ok(response);
        }

        #endregion
    }
}
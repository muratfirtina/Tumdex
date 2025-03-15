using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Application.Abstraction.Services;
using Application.Abstraction.Services.Authentication;
using Application.Abstraction.Services.Email;
using Application.Abstraction.Services.Tokens;
using Application.Abstraction.Services.Utilities;
using Application.Consts;
using Application.CustomAttributes;
using Application.Enums;
using Application.Exceptions;
using Application.Features.Users.Commands.ActivationCode.ResendActivationCode;
using Application.Features.Users.Commands.ActivationCode.VerifyActivationCode;
using Application.Features.Users.Commands.LoginUser;
using Application.Features.Users.Commands.LogoutAllDevices;
using Application.Features.Users.Commands.LogoutUser;
using Application.Features.Users.Commands.RefreshTokenLogin;
using Application.Features.Users.Commands.RevokeUserTokens;
using Application.Features.Users.Commands.PasswordReset;
using Application.Features.Users.Commands.VerifyResetPasswordToken;
using Domain.Identity;
using MassTransit.Mediator;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace WebAPI.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthenticationController : BaseController
    {
        private readonly ILogger<AuthenticationController> _logger;
        private readonly IMetricsService _metricsService;
        private readonly ITokenService _tokenService;

        public AuthenticationController(
            ILogger<AuthenticationController> logger,
            IMetricsService metricsService, ITokenService tokenService)
        {
            _logger = logger;
            _metricsService = metricsService;
            _tokenService = tokenService;
        }

        #region Authentication Operations

        [HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Login([FromBody] LoginUserRequest request)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

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

                // Process the request through the command handler
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

                try
                {
                    _metricsService?.IncrementFailedLogins(ex.GetType().Name, "standard");
                }
                catch
                {
                    // Suppress metrics errors
                }

                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "An error occurred. Please try again later." });
            }
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

        [HttpGet("validate-token")]
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

                // Token geçerliliğini kontrol et
                var tokenService = HttpContext.RequestServices.GetRequiredService<ITokenService>();
                var tokenHandlerService = HttpContext.RequestServices.GetRequiredService<ITokenHandler>();

                // Kullanıcının engellenip engellenmediğini kontrol et
                bool isBlocked = await tokenHandlerService.IsUserBlockedAsync(userId);
                if (isBlocked)
                {
                    return Unauthorized(new { isValid = false, error = "User is blocked" });
                }

                // Token'ın iptal edilip edilmediğini Redis üzerinden kontrol et
                string revokeKey = $"UserTokensRevoked:{userId}";
                var cache = HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
                string? revokedTimeString = await cache.GetStringAsync(revokeKey);

                if (!string.IsNullOrEmpty(revokedTimeString))
                {
                    var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                    var jwtTokenHandler = new JwtSecurityTokenHandler();
                    var jwtToken = jwtTokenHandler.ReadJwtToken(token);

                    DateTime tokenIssuedAt = jwtToken.IssuedAt;

                    if (DateTime.TryParse(revokedTimeString, out DateTime revokedTime) && tokenIssuedAt < revokedTime)
                    {
                        return Unauthorized(new { isValid = false, error = "Token has been revoked" });
                    }
                }

                return Ok(new { isValid = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token doğrulama hatası");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { isValid = false, error = "An error occurred during token validation" });
            }
        }

        [HttpPost("admin/users/{userId}/revoke-tokens")]
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

        #endregion

        #region Account Management

        [HttpPost("password-reset")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> PasswordReset([FromBody] PasswordResetRequest request)
        {
            var response = await Mediator.Send(request);
            return Ok(response);
        }

        [HttpPost("verify-reset-password-token")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> VerifyResetPasswordToken([FromBody] VerifyResetPasswordTokenRequest request)
        {
            var response = await Mediator.Send(request);
            return Ok(response);
        }

        [HttpPost("verify-activation-code")]
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

            // Attempt count check
            string rateLimitKey = $"activation_attempts_{command.UserId}";
            var cacheService = HttpContext.RequestServices.GetRequiredService<ICacheService>();

            // Get current attempt count
            var attemptCount = await cacheService.GetCounterAsync(rateLimitKey);

            // Log current counter for debugging
            _logger.LogDebug("Current attempt count for user {UserId}: {AttemptCount}", command.UserId, attemptCount);

            // Block if exceeded 5 attempts
            if (attemptCount >= 5)
            {
                // Invalidation: Invalidate this user's activation code and token
                // To do this, remove tokens and code from cache
                await cacheService.RemoveAsync($"email_activation_code_{command.UserId}");

                // Also clear the user's activation token cache if it exists
                await cacheService.RemoveAsync($"activation_token_{command.UserId}");

                return StatusCode(StatusCodes.Status429TooManyRequests, new
                {
                    message =
                        "Too many failed attempts. Your activation code has been invalidated. Please request a new activation code.",
                    exceeded = true,
                    requiresNewCode = true
                });
            }

            // Verify activation code
            var result = await Mediator.Send(command);

            if (result.Verified)
            {
                // Clean all relevant cache data on successful verification

                // 1. Clear attempt counter
                await cacheService.RemoveAsync(rateLimitKey);

                // 2. Remove activation code from cache (to prevent reuse)
                await cacheService.RemoveAsync($"email_activation_code_{command.UserId}");

                // 3. Also clear activation token if it exists
                await cacheService.RemoveAsync($"activation_token_{command.UserId}");

                // 4. Leave URL validation marker for this user
                // This prevents reuse of the same URL
                await cacheService.SetAsync($"activation_completed_{command.UserId}", true, TimeSpan.FromDays(90));

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
                await cacheService.IncrementAsync(rateLimitKey, 1, TimeSpan.FromMinutes(30));

                // Get updated counter
                var updatedCount = await cacheService.GetCounterAsync(rateLimitKey);
                var remainingAttempts = Math.Max(0, 5 - updatedCount);

                return BadRequest(new
                {
                    message = $"Invalid activation code. You have {remainingAttempts} attempts remaining.",
                    verified = false,
                    remainingAttempts = remainingAttempts
                });
            }
        }

        [HttpPost("resend-activation-code")]
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
                // Create a more reliable key for rate limiting
                string rateLimitKey = $"activation_resend_{command.Email.ToLower()}";
                var cacheService = HttpContext.RequestServices.GetRequiredService<ICacheService>();

                // Maximum 3 resend requests (within 1 hour)
                var resendCount = await cacheService.GetCounterAsync(rateLimitKey);
                if (resendCount >= 3)
                {
                    return StatusCode(StatusCodes.Status429TooManyRequests, new
                    {
                        message = "Too many code requests. Please try again after 1 hour.",
                        success = false
                    });
                }

                // Find user
                var userService = HttpContext.RequestServices.GetRequiredService<IUserService>();
                AppUser user;

                try
                {
                    user = await userService.GetUserByEmailAsync(command.Email);
                }
                catch (NotFoundUserExceptions)
                {
                    // User not found but don't explicitly state this (for security reasons)
                    return Ok(new { message = "New activation code sent", success = true });
                }

                // Check if user's email is already confirmed
                if (user.EmailConfirmed)
                {
                    // Email already confirmed
                    return Ok(new { message = "Your email is already confirmed. You can login.", success = true });
                }

                // Clear existing failed attempt records
                string attemptKey = $"activation_attempts_{user.Id}";
                await cacheService.RemoveAsync(attemptKey);

                // Generate new activation code
                var authService = HttpContext.RequestServices.GetRequiredService<IAuthService>();
                var activationCode = await _tokenService.GenerateActivationCodeAsync(user.Id);

                // Send email directly, not as a background task
                var emailService = HttpContext.RequestServices.GetRequiredService<IAccountEmailService>();
                await emailService.SendEmailActivationCodeAsync(user.Email, user.Id, activationCode);

                // Increment rate limiter
                await cacheService.IncrementAsync(rateLimitKey, 1, TimeSpan.FromHours(1));

                return Ok(new { message = "New activation code has been sent to your email address.", success = true });
            }
            catch (Exception ex)
            {
                // Log error securely
                HttpContext.RequestServices.GetRequiredService<ILogger<AuthenticationController>>()
                    .LogError(ex, "Error resending activation code to {Email}", command.Email);

                return StatusCode(StatusCodes.Status500InternalServerError,
                    new
                    {
                        message = "An error occurred while sending the activation code. Please try again later.",
                        success = false
                    });
            }
        }


        [HttpGet("verify-activation-token")]
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
                string ipAddress = getIpAddress();
                string rateLimitKey = $"token_verify_{ipAddress}";
                var cacheService = HttpContext.RequestServices.GetRequiredService<ICacheService>();

                // Accept at most 10 requests per minute per IP
                int requestCount = await cacheService.GetCounterAsync(rateLimitKey);
                if (requestCount >= 10)
                {
                    return StatusCode(StatusCodes.Status429TooManyRequests, new
                    {
                        success = false,
                        message = "Too many token verification requests sent. Please try again after a while."
                    });
                }

                // Increment request counter (for 1 minute)
                await cacheService.IncrementAsync(rateLimitKey, 1, TimeSpan.FromMinutes(1));

                // Decode token
                // Base64Url-decode
                string decodedToken;
                try
                {
                    _logger.LogDebug("Attempting to decode token");
                    decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
                    _logger.LogDebug("Token successfully decoded: {Length} characters", decodedToken?.Length ?? 0);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Token decode error");
                    return BadRequest(new { success = false, message = "Invalid token format" });
                }

                // Try to resolve data in token
                try
                {
                    var encryptionService = HttpContext.RequestServices.GetRequiredService<IEncryptionService>();
                    string decryptedJson = await encryptionService.DecryptAsync(decodedToken);

                    // Log JSON content for debugging
                    _logger.LogDebug("Decrypted JSON content: {Content}", decryptedJson);

                    JsonDocument tokenData = JsonDocument.Parse(decryptedJson);

                    // Check for properties' existence and use default values
                    string userId = "";
                    string email = "";

                    if (tokenData.RootElement.TryGetProperty("userId", out var userIdProp))
                        userId = userIdProp.GetString();
                    else if (tokenData.RootElement.TryGetProperty("userId", out userIdProp))
                        userId = userIdProp.GetString();

                    if (tokenData.RootElement.TryGetProperty("email", out var emailProp))
                        email = emailProp.GetString();
                    else if (tokenData.RootElement.TryGetProperty("email", out emailProp))
                        email = emailProp.GetString();

                    if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
                    {
                        _logger.LogWarning("Token valid but does not contain required fields: {JSON}", decryptedJson);
                        return BadRequest(new { success = false, message = "Invalid token content" });
                    }

                    // Optional: Check if token has expired
                    if (tokenData.RootElement.TryGetProperty("expires", out var expiryProp))
                    {
                        long expiryTimestamp = expiryProp.GetInt64();
                        DateTimeOffset expiryTime = DateTimeOffset.FromUnixTimeSeconds(expiryTimestamp);

                        if (expiryTime < DateTimeOffset.UtcNow)
                        {
                            return BadRequest(new { success = false, message = "Token has expired" });
                        }
                    }

                    // Check if activation was previously completed
                    var activationCompletedKey = $"activation_completed_{userId}";
                    var activationCompleted = await cacheService.TryGetValueAsync<bool>(activationCompletedKey);

                    if (activationCompleted.success && activationCompleted.value)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message =
                                "This activation link has already been used. You can go to the login page and sign in to your account.",
                            alreadyActivated = true
                        });
                    }

                    // Token is valid
                    return Ok(new
                    {
                        success = true,
                        userId = userId,
                        email = email
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Token content could not be resolved: {Message}", ex.Message);
                    return BadRequest(new { success = false, message = "Invalid or expired token" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "General token verification error");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { success = false, message = "An error occurred while processing the token" });
            }
        }

        /*[HttpPost("verify-activation-code")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> VerifyActivationCode([FromBody] VerifyActivationCodeCommand command)
        {
            if (string.IsNullOrEmpty(command.UserId) || string.IsNullOrEmpty(command.Code))
            {
                return BadRequest(new { message = "UserId ve aktivasyon kodu gereklidir." });
            }

            var result = await Mediator.Send(command);

            if (result.Verified)
            {
                return Ok(new { message = "E-posta adresiniz başarıyla doğrulandı." });
            }

            return BadRequest(new { message = "Geçersiz aktivasyon kodu. Lütfen tekrar deneyin." });
        }

        [HttpPost("resend-activation-code")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResendActivationCode([FromBody] ResendActivationCodeCommand command)
        {
            if (string.IsNullOrEmpty(command.Email))
            {
                return BadRequest(new { message = "E-posta adresi gereklidir." });
            }

            var result = await Mediator.Send(command);

            return Ok(new { message = "Yeni aktivasyon kodu e-posta adresinize gönderildi." });
        }*/

        #endregion
    }
}
/*using System.Collections.Concurrent;
using System.Diagnostics;
using Application.Abstraction.Services;
using Application.Features.Users.Commands.ConfirmEmail;
using Application.Features.Users.Commands.LoginUser;
using Application.Features.Users.Commands.LogoutUser;
using Application.Features.Users.Commands.PasswordReset;
using Application.Features.Users.Commands.RefreshTokenLogin;
using Application.Features.Users.Commands.ResendConfirmationEmail;
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
        private static readonly ConcurrentDictionary<string, DateTime> _emailRateLimits = new();
        private static readonly TimeSpan _emailCooldown = TimeSpan.FromSeconds(20);

        public AuthController(IMetricsService metricsService)
        {
            _metricsService = metricsService;
        }
        // Email hız sınırlaması kontrolü
        private (bool isLimited, int secondsRemaining) CheckEmailRateLimit(string email)
        {
            if (_emailRateLimits.TryGetValue(email, out DateTime lastSentTime))
            {
                TimeSpan elapsed = DateTime.UtcNow - lastSentTime;
                if (elapsed < _emailCooldown)
                {
                    int secondsRemaining = (int)(_emailCooldown - elapsed).TotalSeconds + 1;
                    return (true, secondsRemaining);
                }
            }
            return (false, 0);
        }

        // Email gönderim zamanını güncelle
        private void UpdateEmailRateLimit(string email)
        {
            _emailRateLimits[email] = DateTime.UtcNow;
            
            // Eski kayıtları temizle (2 saatten eski olanları)
            var keysToRemove = _emailRateLimits
                .Where(kvp => DateTime.UtcNow - kvp.Value > TimeSpan.FromHours(2))
                .Select(kvp => kvp.Key)
                .ToList();
                
            foreach (var key in keysToRemove)
            {
                _emailRateLimits.TryRemove(key, out _);
            }
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> Login(LoginUserRequest loginUserRequest)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var response = await Mediator.Send(loginUserRequest);
                stopwatch.Stop();

                try
                {
                    // Başarılı login metriği
                    _metricsService?.IncrementUserLogins("jwt", "standard");
                    _metricsService?.UpdateActiveUsers("authenticated", 1);
                    _metricsService?.RecordRequestDuration(
                        "auth",
                        "login",
                        stopwatch.Elapsed.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    // Metrics hatalarını yut
                    System.Diagnostics.Debug.WriteLine($"Metrics error: {ex.Message}");
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                try
                {
                    _metricsService?.IncrementFailedLogins(
                        ex.GetType().Name,
                        "standard");
                }
                catch
                {
                    // Metrics hatalarını yut
                }
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
        
        // Email doğrulama endpointi
        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail([FromQuery] string userId, [FromQuery] string token)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                
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
        
        // Doğrulama e-postasını yeniden gönderme endpoint'i
        [HttpPost("resend-confirmation-email")]
        public async Task<IActionResult> ResendConfirmationEmail([FromBody] ResendConfirmationEmailCommand command)
        {
            try
            {
                // Hız sınırlaması kontrolü
                var (isLimited, secondsRemaining) = CheckEmailRateLimit(command.Email);
                if (isLimited)
                {
                    return StatusCode(StatusCodes.Status429TooManyRequests, 
                        new { message = $"Please wait {secondsRemaining} seconds before requesting another email." });
                }
                
                var stopwatch = Stopwatch.StartNew();
                
                await Mediator.Send(command);
                
                // Email gönderimi başarılı olursa rate limit bilgisini güncelle
                UpdateEmailRateLimit(command.Email);
                
                stopwatch.Stop();
                
                try
                {
                    // Wrap metrics in try/catch to prevent failures from affecting the main operation
                    _metricsService?.RecordRequestDuration(
                        "auth",
                        "resend_confirmation",
                        stopwatch.Elapsed.TotalMilliseconds);
                    
                    _metricsService?.IncrementCounter("email_confirmation_resend", "success");
                }
                catch (Exception metricEx)
                {
                    // Log the metrics error but don't throw it
                    System.Diagnostics.Debug.WriteLine($"Metrics error: {metricEx.Message}");
                }
                
                // E-posta adresi sistemde yoksa bile aynı cevabı veriyoruz (güvenlik için)
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
                
                // Hata detaylarını kullanıcıya göstermiyoruz
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { message = "An error occurred. Please try again later." });
            }
        }
    }
}*/
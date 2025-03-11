using System;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Application.Abstraction.Services;
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
using Application.Features.Users.Commands.ConfirmEmail;
using Application.Features.Users.Commands.ResendConfirmationEmail;
using Application.Features.Users.Commands.PasswordReset;
using Application.Features.Users.Commands.VerifyResetPasswordToken;
using Domain.Identity;
using MassTransit.Mediator;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace WebAPI.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthenticationController : BaseController
    {
        private readonly ILogger<AuthenticationController> _logger;
        private readonly IMetricsService _metricsService;
        private readonly IAuthService _authService;

        public AuthenticationController(
            ILogger<AuthenticationController> logger,
            IMetricsService metricsService, IAuthService authService)
        {
            _logger = logger;
            _metricsService = metricsService;
            _authService = authService;
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
                        new { error = "Geçersiz kullanıcı adı/e-posta veya şifre" });
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
                    new { error = "Bir hata oluştu. Lütfen daha sonra tekrar deneyin." });
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

        [HttpGet("confirm-email")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
                return BadRequest(new
                {
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
                return Ok(new
                {
                    message =
                        "If your email exists in our system, a confirmation link has been sent. Please check your email."
                });
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
    _logger.LogInformation("Aktivasyon kodu doğrulama isteği: UserId={UserId}, CodeLength={CodeLength}", 
        command.UserId, command.Code?.Length ?? 0);

    if (string.IsNullOrEmpty(command.UserId) || string.IsNullOrEmpty(command.Code))
    {
        _logger.LogWarning("Eksik parametrelerle aktivasyon isteği: UserId={UserId}, CodeProvided={CodeProvided}", 
            command.UserId ?? "null", !string.IsNullOrEmpty(command.Code));
        return BadRequest(new { message = "UserId ve aktivasyon kodu gereklidir." });
    }

    // Deneme sayısı kontrolü
    string rateLimitKey = $"activation_attempts_{command.UserId}";
    var cacheService = HttpContext.RequestServices.GetRequiredService<ICacheService>();
    
    // Mevcut deneme sayısını al
    var attemptCount = await cacheService.GetCounterAsync(rateLimitKey);
    
    // Debug için mevcut sayacı logla
    _logger.LogDebug("Kullanıcı {UserId} için mevcut deneme sayısı: {AttemptCount}", command.UserId, attemptCount);
    
    // 5 denemeyi aştıysa engelle
    if (attemptCount >= 5)
    {
        // Geçersiz kılma: Bu kullanıcının aktivasyon kodunu ve token'ını geçersiz kıl
        // Bunu yapmak için, token'ları ve kodu önbellekten sil
        await cacheService.RemoveAsync($"email_activation_code_{command.UserId}");
        
        // Ayrıca, varsa kullanıcının activation token cache'ini de temizle
        await cacheService.RemoveAsync($"activation_token_{command.UserId}");
        
        return StatusCode(StatusCodes.Status429TooManyRequests, new 
        { 
            message = "Çok fazla başarısız deneme. Aktivasyon kodunuz geçersiz kılındı. Lütfen yeni bir aktivasyon kodu talep edin.",
            exceeded = true,
            requiresNewCode = true
        });
    }
    
    // Aktivasyon kodunu doğrula
    var result = await Mediator.Send(command);
    
    if (result.Verified)
    {
        // Başarılı doğrulamada tüm ilgili önbellek verilerini temizle
        
        // 1. Deneme sayacını temizle
        await cacheService.RemoveAsync(rateLimitKey);
        
        // 2. Aktivasyon kodunu önbellekten sil (tekrar kullanımı engellemek için)
        await cacheService.RemoveAsync($"email_activation_code_{command.UserId}");
        
        // 3. Varsa aktivasyon token'ını da temizle
        await cacheService.RemoveAsync($"activation_token_{command.UserId}");
        
        // 4. Bu kullanıcı için URL doğrulama işaretçisi bırak
        // Bu, aynı URL'nin tekrar kullanılmasını engeller
        await cacheService.SetAsync($"activation_completed_{command.UserId}", true, TimeSpan.FromDays(90));
        
        _logger.LogInformation("Kullanıcı {UserId} için aktivasyon tamamlandı ve URL/kod imha edildi", command.UserId);
        
        return Ok(new { 
            message = "E-posta adresiniz başarıyla doğrulandı.", 
            verified = true 
        });
    }
    else
    {
        // Başarısız denemede sayacı artır (30 dakika süreyle)
        await cacheService.IncrementAsync(rateLimitKey, 1, TimeSpan.FromMinutes(30));
        
        // Güncel sayacı al
        var updatedCount = await cacheService.GetCounterAsync(rateLimitKey);
        var remainingAttempts = Math.Max(0, 5 - updatedCount);
        
        return BadRequest(new { 
            message = $"Geçersiz aktivasyon kodu. {remainingAttempts} deneme hakkınız kaldı.", 
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
                return BadRequest(new { message = "E-posta adresi gereklidir." });
            }

            try
            {
                // Rate limiting için daha güvenilir bir anahtar oluştur
                string rateLimitKey = $"activation_resend_{command.Email.ToLower()}";
                var cacheService = HttpContext.RequestServices.GetRequiredService<ICacheService>();

                // En fazla 3 yeniden gönderme isteği (1 saat içinde)
                var resendCount = await cacheService.GetCounterAsync(rateLimitKey);
                if (resendCount >= 3)
                {
                    return StatusCode(StatusCodes.Status429TooManyRequests, new
                    {
                        message = "Çok fazla kod talebi. Lütfen 1 saat sonra tekrar deneyin."
                    });
                }

                // Kullanıcıyı bul
                var userService = HttpContext.RequestServices.GetRequiredService<IUserService>();
                AppUser user;

                try
                {
                    user = await userService.GetUserByEmailAsync(command.Email);
                }
                catch (NotFoundUserExceptions)
                {
                    // Kullanıcı bulunamadı ama bunu açıkça belirtme (güvenlik nedeniyle)
                    return Ok(new { message = "Yeni aktivasyon kodu gönderildi" });
                }

                // Kullanıcının e-posta onayını kontrol et
                if (user.EmailConfirmed)
                {
                    // E-posta zaten onaylanmış
                    return Ok(new { message = "E-posta adresiniz zaten onaylanmış. Giriş yapabilirsiniz." });
                }

                // Mevcut hatalı giriş kayıtlarını temizle
                string attemptKey = $"activation_attempts_{user.Id}";
                await cacheService.RemoveAsync(attemptKey);

                // Yeni aktivasyon kodu oluştur
                var authService = HttpContext.RequestServices.GetRequiredService<IAuthService>();
                var activationCode = await authService.GenerateActivationCodeAsync(user.Id);

                // E-postayı doğrudan gönder, background task olarak değil
                var emailService = HttpContext.RequestServices.GetRequiredService<IAccountEmailService>();
                await emailService.SendEmailActivationCodeAsync(user.Email, user.Id, activationCode);

                // Rate limiter'ı artır
                await cacheService.IncrementAsync(rateLimitKey, 1, TimeSpan.FromHours(1));

                return Ok(new { message = "Yeni aktivasyon kodu e-posta adresinize gönderildi." });
            }
            catch (Exception ex)
            {
                // Hatayı güvenli bir şekilde logla
                HttpContext.RequestServices.GetRequiredService<ILogger<AuthenticationController>>()
                    .LogError(ex, "Error resending activation code to {Email}", command.Email);

                return StatusCode(StatusCodes.Status500InternalServerError,
                    new
                    {
                        message = "Aktivasyon kodu gönderilirken bir hata oluştu. Lütfen daha sonra tekrar deneyin."
                    });
            }
        }


        [HttpGet("verify-activation-token")]
public async Task<IActionResult> VerifyActivationToken([FromQuery] string token)
{
    _logger.LogInformation("Token doğrulama isteği alındı: {TokenLength} karakter", token?.Length ?? 0);
    if (string.IsNullOrEmpty(token))
    {
        return BadRequest(new { success = false, message = "Token gereklidir" });
    }

    try {
        // Rate limiting uygula
        string ipAddress = getIpAddress();
        string rateLimitKey = $"token_verify_{ipAddress}";
        var cacheService = HttpContext.RequestServices.GetRequiredService<ICacheService>();
        
        // Her IP için dakikada en fazla 10 istek kabul et
        int requestCount = await cacheService.GetCounterAsync(rateLimitKey);
        if (requestCount >= 10)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { 
                success = false, 
                message = "Çok fazla token doğrulama isteği gönderildi. Lütfen bir süre sonra tekrar deneyin." 
            });
        }
        
        // İstek sayacını artır (1 dakika süreyle)
        await cacheService.IncrementAsync(rateLimitKey, 1, TimeSpan.FromMinutes(1));
        
        // Token'ı çöz
        // Base64Url-decode
        string decodedToken;
        try
        {
            _logger.LogDebug("Token decode edilmeye çalışılıyor");
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            _logger.LogDebug("Token başarıyla decode edildi: {Length} karakter", decodedToken?.Length ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token decode hatası");
            return BadRequest(new { success = false, message = "Geçersiz token formatı" });
        }
        
        // Token içindeki verileri çözmeyi dene
        try
        {
            var encryptionService = HttpContext.RequestServices.GetRequiredService<IEncryptionService>();
            string decryptedJson = await encryptionService.DecryptAsync(decodedToken);
            
            // Debug için JSON içeriğini logla
            _logger.LogDebug("Çözülen JSON içeriği: {Content}", decryptedJson);
            
            JsonDocument tokenData = JsonDocument.Parse(decryptedJson);
            
            // Özelliklerin varlığını kontrol et ve varsayılan değerler kullan
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
                _logger.LogWarning("Token geçerli ancak gerekli alanları içermiyor: {JSON}", decryptedJson);
                return BadRequest(new { success = false, message = "Geçersiz token içeriği" });
            }
            
            // İsteğe bağlı: Token süresinin dolup dolmadığını kontrol et
            if (tokenData.RootElement.TryGetProperty("expires", out var expiryProp))
            {
                long expiryTimestamp = expiryProp.GetInt64();
                DateTimeOffset expiryTime = DateTimeOffset.FromUnixTimeSeconds(expiryTimestamp);
                
                if (expiryTime < DateTimeOffset.UtcNow)
                {
                    return BadRequest(new { success = false, message = "Token süresi dolmuş" });
                }
            }
            
            // Aktivasyonun daha önce tamamlanıp tamamlanmadığını kontrol et
            var activationCompletedKey = $"activation_completed_{userId}";
            var activationCompleted = await cacheService.TryGetValueAsync<bool>(activationCompletedKey);
            
            if (activationCompleted.success && activationCompleted.value)
            {
                return BadRequest(new { 
                    success = false, 
                    message = "Bu aktivasyon linki zaten kullanılmış. Giriş sayfasına giderek hesabınıza giriş yapabilirsiniz.",
                    alreadyActivated = true
                });
            }
            
            // Token geçerli
            return Ok(new { 
                success = true, 
                userId = userId, 
                email = email 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token içeriği çözülemedi: {Message}", ex.Message);
            return BadRequest(new { success = false, message = "Geçersiz veya süresi dolmuş token" });
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Token doğrulama genel hatası");
        return StatusCode(StatusCodes.Status500InternalServerError, 
            new { success = false, message = "Token işlenirken bir hata oluştu" });
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
/*using System;
using System.Security.Claims;
using Application.Features.Users.Commands.LoginUser;
using Application.Features.Users.Commands.LogoutAllDevices;
using Application.Features.Users.Commands.LogoutUser;
using Application.Features.Users.Commands.RefreshTokenLogin;
using Application.Features.Users.Commands.RevokeUserTokens;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TokenController : BaseController
    {
        private readonly IMediator _mediator;
        private readonly ILogger<TokenController> _logger;
        
        public TokenController(
            IMediator mediator,
            ILogger<TokenController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }
        
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginUserRequest command)
        {
            try 
            {
                var result = await _mediator.Send(command);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login hatası");
                return StatusCode(StatusCodes.Status401Unauthorized, 
                    new { error = "Kullanıcı adı veya şifre hatalı" });
            }
        }
        
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenLoginRequest command)
        {
            if (string.IsNullOrEmpty(command.RefreshToken))
            {
                return BadRequest(new { error = "Refresh token gereklidir" });
            }
            
            try
            {
                command.IpAddress = getIpAddress();
                command.UserAgent = Request.Headers["User-Agent"].ToString() ?? "Unknown";
                
                var result = await _mediator.Send(command);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Refresh token hatası");
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token yenileme hatası");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "Token yenilenirken bir hata oluştu" });
            }
        }
        
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] LogoutUserCommand command)
        {
            if (string.IsNullOrEmpty(command.RefreshToken))
            {
                return BadRequest(new { error = "Refresh token gereklidir" });
            }
            
            try
            {
                command.IpAddress = getIpAddress();
                
                var result = await _mediator.Send(command);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout hatası");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "Çıkış yaparken bir hata oluştu" });
            }
        }
        
        [HttpPost("logout-all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> LogoutAllDevices()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest(new { error = "Kullanıcı kimliği bulunamadı" });
                }
                
                var command = new LogoutAllDevicesCommand
                {
                    UserId = userId,
                    IpAddress = getIpAddress(),
                    Reason = "Logout from all devices"
                };
                
                var result = await _mediator.Send(command);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tüm cihazlardan logout hatası");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "Tüm cihazlardan çıkış yaparken bir hata oluştu" });
            }
        }
        
        [HttpPost("admin/users/{userId}/revoke-tokens")]
        [Authorize(Roles = "Admin")]
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
        
                var result = await _mediator.Send(command);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin tarafından token iptal hatası");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "Kullanıcı token'ları iptal edilirken bir hata oluştu" });
            }
        }
        
        
    }
}*/
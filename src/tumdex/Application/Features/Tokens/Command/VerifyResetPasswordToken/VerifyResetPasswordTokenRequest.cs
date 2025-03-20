using Application.Abstraction.Services;
using Application.Abstraction.Services.Tokens;
using Application.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Tokens.Command.VerifyResetPasswordToken;

public class VerifyResetPasswordTokenRequest : IRequest<VerifyResetPasswordTokenResponse>
{
    public string UserId { get; set; }
    public string ResetToken { get; set; }
    
    public class VerifyResetPasswordTokenCommandHandler : IRequestHandler<VerifyResetPasswordTokenRequest, VerifyResetPasswordTokenResponse>
    {
        private readonly ITokenService _tokenService;
        private readonly IUserService _userService;
        private readonly ILogger<VerifyResetPasswordTokenCommandHandler> _logger;

        public VerifyResetPasswordTokenCommandHandler(
            ITokenService tokenService,
            IUserService userService,
            ILogger<VerifyResetPasswordTokenCommandHandler> logger)
        {
            _tokenService = tokenService;
            _userService = userService;
            _logger = logger;
        }
    
        public async Task<VerifyResetPasswordTokenResponse> Handle(VerifyResetPasswordTokenRequest request, CancellationToken cancellationToken)
        {
            try
            {
                // Token geçerliliğini kontrol et
                bool isTokenValid = await _tokenService.VerifyResetPasswordTokenAsync(request.UserId, request.ResetToken);
                
                // Varsayılan yanıt
                var response = new VerifyResetPasswordTokenResponse
                {
                    TokenValid = isTokenValid,
                    UserId = request.UserId,
                    Email = string.Empty
                };

                // Eğer token geçerliyse, kullanıcı bilgilerini ekle
                if (isTokenValid)
                {
                    try
                    {
                        var user = await _userService.GetUserByIdAsync(request.UserId);
                        response.Email = user.Email;
                        
                        _logger.LogInformation("Şifre sıfırlama tokeni doğrulandı: UserID={UserId}", request.UserId);
                    }
                    catch (NotFoundUserExceptions)
                    {
                        // Kullanıcı bulunamadıysa token'ı geçersiz olarak işaretle
                        _logger.LogWarning("Şifre sıfırlama token'ı geçerli ancak kullanıcı bulunamadı: UserID={UserId}", request.UserId);
                        response.TokenValid = false;
                    }
                    catch (Exception ex)
                    {
                        // Diğer hatalar
                        _logger.LogError(ex, "Kullanıcı bilgileri alınırken hata oluştu: UserID={UserId}", request.UserId);
                        response.TokenValid = false;
                    }
                }
                else
                {
                    _logger.LogWarning("Geçersiz şifre sıfırlama tokeni: UserID={UserId}", request.UserId);
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Şifre sıfırlama token doğrulama işlemi sırasında hata: UserID={UserId}", request.UserId);
                
                // Hata durumunda varsayılan olarak geçersiz token yanıtı döndür
                return new VerifyResetPasswordTokenResponse
                {
                    TokenValid = false,
                    UserId = request.UserId,
                    Email = string.Empty
                };
            }
        }
    }
}
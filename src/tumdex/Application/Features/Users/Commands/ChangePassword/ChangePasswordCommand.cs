using Application.Exceptions;
using Application.Features.Users.Exceptions;
using Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Application.Features.Users.Commands.ChangePassword;

public class ChangePasswordCommand : IRequest<ChangePasswordResponse>
{
    public string CurrentPassword { get; set; }
    public string NewPassword { get; set; }

    public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, ChangePasswordResponse>
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ChangePasswordCommandHandler(UserManager<AppUser> userManager, IHttpContextAccessor httpContextAccessor)
        {
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ChangePasswordResponse> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
        {
            var userName = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            if (string.IsNullOrEmpty(userName))
                throw new AuthenticationErrorException();

            var user = await _userManager.FindByNameAsync(userName);
            if (user == null)
                throw new NotFoundUserExceptions();

            // Mevcut şifreyi doğrula
            var isValidPassword = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);
            if (!isValidPassword)
                throw new AuthenticationErrorException("Current password is incorrect.");

            // Yeni şifreyi güncelle
            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
                throw new PasswordChangeFailedException(result.Errors.Select(e => e.Description).ToList());

            // Güvenlik damgasını güncelle
            await _userManager.UpdateSecurityStampAsync(user);

            return new ChangePasswordResponse
            {
                Message = "Password changed successfully"
            };
        }
    }
}
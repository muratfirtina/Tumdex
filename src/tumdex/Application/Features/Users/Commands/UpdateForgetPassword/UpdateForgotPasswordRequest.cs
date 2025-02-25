using Application.Abstraction.Services;
using Application.Exceptions;
using MediatR;

namespace Application.Features.Users.Commands.UpdateForgetPassword;

public class UpdateForgotPasswordRequest: IRequest<UpdateForgotPasswordResponse>
{
    public string UserId { get; set; }
    public string ResetToken { get; set; }
    public string Password { get; set; }
    public string PasswordConfirm { get; set; }
    
    public class UpdateForgotPasswordCommandHandler: IRequestHandler<UpdateForgotPasswordRequest, UpdateForgotPasswordResponse>
    {
        readonly IUserService _userService;

        public UpdateForgotPasswordCommandHandler(IUserService userService)
        {
            _userService = userService;
        }

        public async Task<UpdateForgotPasswordResponse> Handle(UpdateForgotPasswordRequest request, CancellationToken cancellationToken)
        {
            if (!request.Password.Equals(request.PasswordConfirm))
            {
                throw new ResetPasswordException("Passwords do not match. Please confirm password.");
            }
        
            await _userService.UpdateForgotPasswordAsync(request.UserId, request.ResetToken, request.Password);
            return new();
        }
    }
    
}
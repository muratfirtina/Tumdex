using Application.Features.Events.User.UserRegister;
using Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Application.Features.Users.Commands.CreateUser;

public class CreateUserCommand : IRequest<CreatedUserResponse>
{
    public string NameSurname { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public string ConfirmPassword { get; set; }
    
    public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, CreatedUserResponse>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IMediator _mediator;
    private readonly ILogger<CreateUserCommandHandler> _logger;

    public CreateUserCommandHandler(
        UserManager<AppUser> userManager,
        IMediator mediator,
        ILogger<CreateUserCommandHandler> logger)
    {
        _userManager = userManager;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<CreatedUserResponse> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var response = new CreatedUserResponse();

        try
        {
            // Kullanıcı oluşturma
            var user = new AppUser
            {
                Id = Guid.NewGuid().ToString(),
                NameSurname = request.NameSurname,
                UserName = request.UserName,
                Email = request.Email
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                response.IsSuccess = false;
                response.Message = string.Join("\n", createResult.Errors.Select(e => $"{e.Code} - {e.Description}"));
                return response;
            }

            // Rol atama
            var role = request.UserName.ToLower() == "karafirtina" ? "Admin" : "User";
            var roleResult = await _userManager.AddToRoleAsync(user, role);

            if (!roleResult.Succeeded)
            {
                response.IsSuccess = false;
                response.Message = $"User created  but {role} role could not be added.";
                return response;
            }

            // Newsletter kaydı - hata olsa bile kullanıcı kaydını etkilemeyecek
            try
            {
                await _mediator.Publish(new UserRegisteredEvent(user), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Newsletter subscription failed for user {Email} but user registration completed successfully", user.Email);
            }

            response.IsSuccess = true;
            response.Message = $"User created and {role} role added.";
            return response;
        }
        catch (Exception ex)
        {
            response.IsSuccess = false;
            response.Message = $"An unexpected error occurred: {ex.Message}";
            return response;
        }
    }
}
}
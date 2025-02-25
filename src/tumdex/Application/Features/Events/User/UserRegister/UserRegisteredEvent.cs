using Domain.Identity;
using MediatR;

namespace Application.Features.Events.User.UserRegister;

public class UserRegisteredEvent : INotification
{
    public AppUser User { get; }

    public UserRegisteredEvent(AppUser user)
    {
        User = user;
    }
}
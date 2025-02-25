using Application.Abstraction.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace SignalR.Hubs;

[Authorize(AuthenticationSchemes = "Admin")]
public class OrderHub : Hub
{
    private readonly ILogger<OrderHub> _logger;
    private readonly IUserService _userService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public OrderHub(
        ILogger<OrderHub> logger,
        IUserService userService,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _userService = userService; 
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            await base.OnConnectedAsync(); // İlk önce base'i çağırıyoruz

            var username = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            if (!string.IsNullOrEmpty(username))
            {
                bool isAdmin = await _userService.IsAdminAsync();
                if (isAdmin)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
                    _logger.LogInformation($"User {username} added to Admins group");
                }
                else 
                {
                    _logger.LogWarning($"Non-admin user {username} attempted to connect");
                    throw new HubException("Only admin users can connect to this hub");
                }
            }
            else
            {
                _logger.LogWarning("Anonymous user attempted to connect");
                throw new HubException("Authentication required");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnConnectedAsync");
            throw; // Hatayı fırlatıyoruz ki client tarafı bu hatayı alabilsin
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var user = Context.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Admins");
                _logger.LogInformation($"User {user.Identity.Name} removed from Admins group");
            }
            await base.OnDisconnectedAsync(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnDisconnectedAsync");
            throw;
        }
    }

    public async Task JoinAdminGroup()
    {
        var user = Context.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new HubException("Unauthorized");
        }

        // Veritabanından admin kontrolü
        bool isAdmin = await _userService.IsAdminAsync();
        if (!isAdmin)
        {
            throw new HubException("User is not an admin");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
        _logger.LogInformation($"User {user.Identity.Name} manually joined Admins group");
    }

    public async Task LeaveAdminGroup()
    {
        var user = Context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Admins");
            _logger.LogInformation($"User {user.Identity.Name} left Admins group");
        }
    }
}
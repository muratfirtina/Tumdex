using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using SignalR.Hubs;

namespace SignalR;

public static class HubRegistration
{
    public static void MapHubs(this WebApplication webApplication)
    {
        webApplication.MapHub<OrderHub>("/order-hub")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = "Admin" });
    }
}
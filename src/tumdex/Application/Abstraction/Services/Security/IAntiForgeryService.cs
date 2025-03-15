using Microsoft.AspNetCore.Http;

namespace Application.Abstraction.Services.Security;

public interface IAntiForgeryService
{
    void GenerateTokens(HttpContext httpContext);
    bool ValidateToken(HttpContext httpContext);
    void ClearTokens(HttpContext httpContext);
}
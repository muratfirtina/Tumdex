using Microsoft.AspNetCore.Http;

namespace Application.Abstraction.Services.Security;

public interface IBrowserFingerprintService
{
    string GenerateFingerprint(HttpContext httpContext);
    bool ValidateFingerprint(HttpContext httpContext, string storedFingerprint);
}
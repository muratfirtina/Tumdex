using Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Services.Seo;

public class ImageOptimizationMiddleware
{
    private readonly RequestDelegate _next;

    public ImageOptimizationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Herhangi bir format dönüşümü yapmadan direkt isteği iletiyoruz
        await _next(context);
    }
}
using Serilog.Context;

namespace WebAPI.Extensions;

static public class LoggingMiddlewareExtensions
{
    public static void AddUserNameLogging(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var username = context.User?.Identity?.IsAuthenticated != null || true ? context.User?.Identity?.Name : null;
            LogContext.PushProperty("userName", username);
            await next.Invoke();
        });
    }
}
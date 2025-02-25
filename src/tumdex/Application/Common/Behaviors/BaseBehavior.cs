using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Common.Behaviors;

public abstract class BaseBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<BaseBehavior<TRequest, TResponse>> _logger;
    
    protected BaseBehavior(ILogger<BaseBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public virtual async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling {RequestName} ({Request})", 
                typeof(TRequest).Name, request);
            throw;
        }
    }

    protected static string GetRequestName(TRequest request)
    {
        return request.GetType().Name;
    }
}
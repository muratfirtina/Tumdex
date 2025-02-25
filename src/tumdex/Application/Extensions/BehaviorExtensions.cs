using Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Extensions;

public static class BehaviorExtensions
{
    public static IServiceCollection AddCustomBehaviors(this IServiceCollection services)
    {
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(MetricsBehavior<,>));
        
        
        // Diğer behavior'ları da buraya ekleyebilirsiniz
        // Örneğin: Validation, Logging, Caching vb.
        
        return services;
    }
}
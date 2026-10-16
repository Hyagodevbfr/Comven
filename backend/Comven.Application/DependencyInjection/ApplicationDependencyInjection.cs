using Comven.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Comven.Application.DependencyInjection;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddScoped<OperationService>();
        services.AddScoped<ClientService>();

        return services;
    }
}
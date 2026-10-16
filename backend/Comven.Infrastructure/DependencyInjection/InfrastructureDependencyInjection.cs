using Comven.Application.Interfaces;
using Comven.Application.Interfaces.Repositories;
using Comven.Application.Interfaces.Services;
using Comven.Infrastructure.Persistence;
using Comven.Infrastructure.Providers;
using Comven.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Comven.Infrastructure.DependencyInjection;

public static class InfrastructureDependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IFinancialTransactionRepository, FinancialTransactionRepository>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IOperationProvider, FakeOperationProvider>();

        services.AddScoped<IClientRepository, ClientRepository>();

        services.AddScoped<IOperationRepository, OperationRepository>();
        services.AddScoped<IStatementRepository, StatementRepository>();

        return services;
    }
}
using Comven.Api.DependencyInjection;
using Comven.Api.Development;
using Comven.Application.DependencyInjection;
using Comven.Application.Interfaces.Services;
using Comven.Infrastructure.DependencyInjection;
using Comven.Infrastructure.Providers;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApi()
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

if (builder.Environment.IsDevelopment())
{
    // Apenas em Development: permite forçar a falha do fornecedor simulado
    // por requisição, para a página de testes de cenários.
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<FakeOperationProvider>();
    builder.Services.AddScoped<IOperationProvider, DevelopmentOperationProvider>();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    app.MapGet("/api/dev/provider-failure", () => Results.Ok(new { available = true }));
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

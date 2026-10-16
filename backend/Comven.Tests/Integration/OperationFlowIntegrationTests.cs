using Comven.Application.DTOs.Requests;
using Comven.Application.Interfaces.Services;
using Comven.Application.Services;
using Comven.Domain.Enums;
using Comven.Infrastructure.Persistence;
using Comven.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Comven.Tests.Integration;

/// <summary>
/// Testes de integração contra um PostgreSQL real (migrations aplicadas).
/// Exigem a variável de ambiente COMVEN_TEST_CONNECTION com a connection string.
/// Cada teste cria seus próprios clientes (nomes/IDs únicos) e os remove ao final.
/// </summary>
[Trait("Category", "Integration")]
public class OperationFlowIntegrationTests : IAsyncLifetime
{
    private const string ConnectionVariable = "COMVEN_TEST_CONNECTION";

    private sealed class StubProvider : IOperationProvider
    {
        private readonly bool _succeeds;

        public StubProvider(bool succeeds) => _succeeds = succeeds;

        public Task<bool> ExecuteAsync(OperationType type, CancellationToken cancellationToken = default)
            => Task.FromResult(_succeeds);
    }

    private readonly List<Guid> _clientIds = new();
    private DbContextOptions<AppDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);

        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException(
                $"Defina a variável de ambiente {ConnectionVariable} com a connection string do PostgreSQL de testes.");

        _options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options;

        try
        {
            await using var context = new AppDbContext(_options);
            await context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Não foi possível conectar ao PostgreSQL usando {ConnectionVariable}. " +
                "Verifique se o banco está no ar (docker compose up -d).", ex);
        }
    }

    public async Task DisposeAsync()
    {
        if (_options is null || _clientIds.Count == 0)
            return;

        await using var context = new AppDbContext(_options);

        await context.FinancialTransactions
            .Where(t => _clientIds.Contains(t.ClientId)).ExecuteDeleteAsync();
        await context.Operations
            .Where(o => _clientIds.Contains(o.ClientId)).ExecuteDeleteAsync();
        await context.Clients
            .Where(c => _clientIds.Contains(c.Id)).ExecuteDeleteAsync();
    }

    // Monta o fluxo real (DbContext, repositórios, UnitOfWork e services); só o fornecedor é simulado.
    private (OperationService Operations, ClientService Clients, AppDbContext Context) CreateServices(bool providerSucceeds)
    {
        var context = new AppDbContext(_options);
        var clients = new ClientRepository(context);
        var unitOfWork = new UnitOfWork(context);

        var operations = new OperationService(
            unitOfWork,
            new StubProvider(providerSucceeds),
            new FinancialTransactionRepository(context),
            new OperationRepository(context),
            clients);

        return (operations, new ClientService(clients, unitOfWork), context);
    }

    private async Task<Guid> CreateClientAsync(ClientService clients, decimal balance)
    {
        var result = await clients.CreateAsync(new CreateClientRequest($"it-{Guid.NewGuid():N}", balance));

        Assert.True(result.IsSuccess, result.Error);

        _clientIds.Add(result.Value!.Id);
        return result.Value.Id;
    }

    private async Task<decimal> GetBalanceAsync(Guid clientId)
    {
        await using var context = new AppDbContext(_options);
        return await context.Clients.Where(c => c.Id == clientId).Select(c => c.Balance).SingleAsync();
    }

    [Fact]
    public async Task Debit_WithSufficientBalance_UpdatesBalanceInDatabase()
    {
        var (operations, clients, context) = CreateServices(providerSucceeds: true);
        await using var _ = context;
        var clientId = await CreateClientAsync(clients, 100m);

        var result = await operations.CreateAsync(
            new CreateOperationRequest(clientId, Guid.NewGuid(), OperationType.VehicleConsulting));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(100m - 12.50m, await GetBalanceAsync(clientId));

        await using var check = new AppDbContext(_options);
        var operation = await check.Operations.SingleAsync(o => o.ClientId == clientId);
        Assert.Equal(OperationStatus.Completed, operation.Status);

        var transaction = await check.FinancialTransactions.SingleAsync(t => t.ClientId == clientId);
        Assert.Equal(FinancialTransactionType.Debit, transaction.Type);
        Assert.Equal(12.50m, transaction.Amount);
    }

    [Fact]
    public async Task Debit_WithInsufficientBalance_LeavesDatabaseUntouched()
    {
        var (operations, clients, context) = CreateServices(providerSucceeds: true);
        await using var _ = context;
        var clientId = await CreateClientAsync(clients, 10m);

        var result = await operations.CreateAsync(
            new CreateOperationRequest(clientId, Guid.NewGuid(), OperationType.DocumentIssuance));

        Assert.True(result.IsFailure);
        Assert.Equal(10m, await GetBalanceAsync(clientId));

        await using var check = new AppDbContext(_options);
        Assert.False(await check.Operations.AnyAsync(o => o.ClientId == clientId));
        Assert.False(await check.FinancialTransactions.AnyAsync(t => t.ClientId == clientId));
    }

    [Fact]
    public async Task ProviderFailure_RefundsBalanceExactlyOnce()
    {
        var (operations, clients, context) = CreateServices(providerSucceeds: false);
        await using var _ = context;
        var clientId = await CreateClientAsync(clients, 100m);

        var result = await operations.CreateAsync(
            new CreateOperationRequest(clientId, Guid.NewGuid(), OperationType.DocumentIssuance));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(100m, await GetBalanceAsync(clientId));

        await using var check = new AppDbContext(_options);
        var operation = await check.Operations.SingleAsync(o => o.ClientId == clientId);
        Assert.Equal(OperationStatus.Failed, operation.Status);

        var transactions = await check.FinancialTransactions
            .Where(t => t.OperationId == operation.Id).ToListAsync();
        Assert.Equal(1, transactions.Count(t => t.Type == FinancialTransactionType.Debit));
        Assert.Equal(1, transactions.Count(t => t.Type == FinancialTransactionType.Refund));
    }
}

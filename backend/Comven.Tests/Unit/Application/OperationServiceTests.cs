using Comven.Application.DTOs.Requests;
using Comven.Application.Exceptions;
using Comven.Application.Interfaces;
using Comven.Application.Interfaces.Repositories;
using Comven.Application.Interfaces.Services;
using Comven.Application.Services;
using Comven.Domain.Entities;
using Comven.Domain.Enums;

namespace Comven.Tests.Unit.Application;

public class OperationServiceTests
{
    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public List<string> Calls { get; } = new();
        public Exception? SaveException { get; set; }

        public int Count(string call) => Calls.Count(c => c == call);

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("Begin");
            return Task.CompletedTask;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("Commit");
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("Rollback");
            return Task.CompletedTask;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("Save");
            if (SaveException is not null)
                throw SaveException;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeClientRepository : IClientRepository
    {
        public bool DebitResult { get; set; } = true;
        public bool RefundResult { get; set; } = true;
        public Client? ExistingClient { get; set; }
        public List<(Guid ClientId, decimal Amount)> Debits { get; } = new();
        public List<(Guid ClientId, decimal Amount)> Refunds { get; } = new();

        public Task<Client?> GetByIdAsync(Guid clientId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingClient);

        public Task AddAsync(Client client, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> TryDebitAsync(Guid clientId, decimal amount, CancellationToken cancellationToken = default)
        {
            Debits.Add((clientId, amount));
            return Task.FromResult(DebitResult);
        }

        public Task<bool> TryRefundAsync(Guid clientId, decimal amount, CancellationToken cancellationToken = default)
        {
            Refunds.Add((clientId, amount));
            return Task.FromResult(RefundResult);
        }
    }

    private sealed class FakeOperationRepository : IOperationRepository
    {
        private readonly Dictionary<Guid, Operation> _store = new();
        // Operações já "visíveis" à consulta por chave de idempotência (as adicionadas pelo serviço não entram aqui).
        private readonly Dictionary<Guid, Operation> _visible = new();

        public bool MarkCompletedResult { get; set; } = true;
        public bool MarkFailedResult { get; set; } = true;
        public int LookupCount { get; private set; }
        public List<Operation> Added { get; } = new();
        public int CompletedCalls { get; private set; }
        public int FailedCalls { get; private set; }

        // Simula uma operação que só fica visível após a primeira consulta (corrida).
        public Operation? VisibleAfterFirstLookup { get; set; }

        public void Seed(Operation operation)
        {
            _store[operation.Id] = operation;
            _visible[operation.Id] = operation;
        }

        public Task<Operation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.GetValueOrDefault(id));

        public Task<Operation?> GetByIdempotencyKeyAsync(Guid clientId, Guid idempotencyKey, CancellationToken cancellationToken = default)
        {
            LookupCount++;

            if (LookupCount > 1 && VisibleAfterFirstLookup is not null)
                Seed(VisibleAfterFirstLookup);

            return Task.FromResult(_visible.Values.FirstOrDefault(o =>
                o.ClientId == clientId && o.IdempotencyKey == idempotencyKey));
        }

        public Task AddAsync(Operation operation, CancellationToken cancellationToken = default)
        {
            Added.Add(operation);
            _store[operation.Id] = operation;
            return Task.CompletedTask;
        }

        public Task<bool> TryMarkAsCompletedAsync(Guid operationId, CancellationToken cancellationToken = default)
        {
            CompletedCalls++;
            if (MarkCompletedResult && _store.TryGetValue(operationId, out var op))
                op.Complete();
            return Task.FromResult(MarkCompletedResult);
        }

        public Task<bool> TryMarkAsFailedAsync(Guid operationId, CancellationToken cancellationToken = default)
        {
            FailedCalls++;
            return Task.FromResult(MarkFailedResult);
        }
    }

    private sealed class FakeTransactionRepository : IFinancialTransactionRepository
    {
        public List<FinancialTransaction> Added { get; } = new();

        public Task AddAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default)
        {
            Added.Add(transaction);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(Guid operationId, FinancialTransactionType type, CancellationToken cancellationToken = default)
            => Task.FromResult(Added.Any(t => t.OperationId == operationId && t.Type == type));
    }

    private sealed class FakeProvider : IOperationProvider
    {
        public bool Result { get; set; } = true;
        public Exception? Exception { get; set; }
        public int Calls { get; private set; }

        public Task<bool> ExecuteAsync(OperationType type, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Exception is not null)
                throw Exception;
            return Task.FromResult(Result);
        }
    }

    private sealed class Harness
    {
        public FakeUnitOfWork UnitOfWork { get; } = new();
        public FakeClientRepository Clients { get; } = new();
        public FakeOperationRepository Operations { get; } = new();
        public FakeTransactionRepository Transactions { get; } = new();
        public FakeProvider Provider { get; } = new();

        public OperationService Service => new(UnitOfWork, Provider, Transactions, Operations, Clients);
    }

    private static CreateOperationRequest Request(OperationType type = OperationType.VehicleConsulting)
        => new(Guid.NewGuid(), Guid.NewGuid(), type);

    // ---------- validação de entrada ----------

    [Fact]
    public async Task CreateAsync_WithEmptyClientId_FailsWithoutTouchingDependencies()
    {
        var h = new Harness();

        var result = await h.Service.CreateAsync(new CreateOperationRequest(Guid.Empty, Guid.NewGuid(), OperationType.VehicleConsulting));

        Assert.True(result.IsFailure);
        Assert.Empty(h.UnitOfWork.Calls);
        Assert.Empty(h.Clients.Debits);
        Assert.Equal(0, h.Provider.Calls);
    }

    [Fact]
    public async Task CreateAsync_WithEmptyIdempotencyKey_Fails()
    {
        var h = new Harness();

        var result = await h.Service.CreateAsync(new CreateOperationRequest(Guid.NewGuid(), Guid.Empty, OperationType.VehicleConsulting));

        Assert.True(result.IsFailure);
        Assert.Empty(h.UnitOfWork.Calls);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidType_FailsWithoutDebit()
    {
        var h = new Harness();

        var result = await h.Service.CreateAsync(Request((OperationType)99));

        Assert.True(result.IsFailure);
        Assert.Equal("Tipo de operação inválido.", result.Error);
        Assert.Empty(h.Clients.Debits);
        Assert.Equal(0, h.Provider.Calls);
    }

    // ---------- saldo insuficiente ----------

    [Fact]
    public async Task CreateAsync_WithInsufficientBalance_FailsRollsBackAndPersistsNothing()
    {
        var h = new Harness();
        h.Clients.DebitResult = false;
        h.Clients.ExistingClient = Client.Create("Maria", 1m).Value;

        var result = await h.Service.CreateAsync(Request());

        Assert.True(result.IsFailure);
        Assert.Equal("Saldo insuficiente.", result.Error);
        Assert.Equal(1, h.UnitOfWork.Count("Rollback"));
        Assert.Equal(0, h.UnitOfWork.Count("Commit"));
        Assert.Empty(h.Operations.Added);
        Assert.Empty(h.Transactions.Added);
        Assert.Equal(0, h.Provider.Calls);
    }

    [Fact]
    public async Task CreateAsync_WhenDebitFailsAndClientDoesNotExist_ReturnsClientNotFound()
    {
        var h = new Harness();
        h.Clients.DebitResult = false;
        h.Clients.ExistingClient = null;

        var result = await h.Service.CreateAsync(Request());

        Assert.True(result.IsFailure);
        Assert.Equal("Cliente não encontrado.", result.Error);
    }

    // ---------- execução bem-sucedida ----------

    [Fact]
    public async Task CreateAsync_WhenProviderSucceeds_DebitsPersistsAndCompletes()
    {
        var h = new Harness();
        var request = Request(OperationType.DocumentIssuance);

        var result = await h.Service.CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(OperationStatus.Completed, result.Value!.Status);
        Assert.Equal(45.00m, result.Value.Amount);

        Assert.Single(h.Clients.Debits);
        Assert.Equal((request.ClientId, 45.00m), h.Clients.Debits[0]);

        var operation = Assert.Single(h.Operations.Added);
        Assert.Equal(request.IdempotencyKey, operation.IdempotencyKey);

        var debit = Assert.Single(h.Transactions.Added);
        Assert.Equal(FinancialTransactionType.Debit, debit.Type);
        Assert.Equal(operation.Id, debit.OperationId);
        Assert.Equal(45.00m, debit.Amount);

        Assert.Equal(new[] { "Begin", "Save", "Commit" }, h.UnitOfWork.Calls);
        Assert.Equal(1, h.Provider.Calls);
        Assert.Equal(1, h.Operations.CompletedCalls);
        Assert.Equal(0, h.Operations.FailedCalls);
        Assert.Empty(h.Clients.Refunds);
    }

    [Fact]
    public async Task CreateAsync_WhenMarkAsCompletedFails_ThrowsInvalidOperation()
    {
        var h = new Harness();
        h.Operations.MarkCompletedResult = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Service.CreateAsync(Request()));
    }

    // ---------- falha do fornecedor / estorno ----------

    [Fact]
    public async Task CreateAsync_WhenProviderFails_MarksFailedAndRefundsInNewTransaction()
    {
        var h = new Harness();
        h.Provider.Result = false;
        var request = Request(OperationType.LicensePlateConsultation);

        var result = await h.Service.CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, h.Operations.FailedCalls);
        Assert.Equal(0, h.Operations.CompletedCalls);

        Assert.Single(h.Clients.Refunds);
        Assert.Equal((request.ClientId, 8.90m), h.Clients.Refunds[0]);

        Assert.Equal(2, h.Transactions.Added.Count);
        var refund = h.Transactions.Added[1];
        Assert.Equal(FinancialTransactionType.Refund, refund.Type);
        Assert.Equal(8.90m, refund.Amount);
        Assert.Equal(h.Operations.Added[0].Id, refund.OperationId);

        Assert.Equal(new[] { "Begin", "Save", "Commit", "Begin", "Save", "Commit" }, h.UnitOfWork.Calls);
    }

    [Fact]
    public async Task CreateAsync_WhenProviderFailsButOperationAlreadyChanged_DoesNotRefund()
    {
        var h = new Harness();
        h.Provider.Result = false;
        h.Operations.MarkFailedResult = false;

        var result = await h.Service.CreateAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.Empty(h.Clients.Refunds);
        Assert.Single(h.Transactions.Added); // apenas o débito
        Assert.Equal(1, h.UnitOfWork.Count("Rollback"));
    }

    [Fact]
    public async Task CreateAsync_WhenRefundFails_RollsBackAndThrows()
    {
        var h = new Harness();
        h.Provider.Result = false;
        h.Clients.RefundResult = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Service.CreateAsync(Request()));

        Assert.Equal(1, h.UnitOfWork.Count("Rollback"));
        Assert.Single(h.Transactions.Added); // estorno não registrado
    }

    [Fact]
    public async Task CreateAsync_WhenProviderThrows_ReturnsOperationWithoutCompletingOrRefunding()
    {
        var h = new Harness();
        h.Provider.Exception = new HttpRequestException("timeout");

        var result = await h.Service.CreateAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.Equal(OperationStatus.Pending, result.Value!.Status);
        Assert.Equal(0, h.Operations.CompletedCalls);
        Assert.Equal(0, h.Operations.FailedCalls);
        Assert.Empty(h.Clients.Refunds);
    }

    // ---------- idempotência ----------

    [Fact]
    public async Task CreateAsync_WithExistingKeyAndSameType_ReturnsExistingWithoutNewDebit()
    {
        var h = new Harness();
        var request = Request(OperationType.VehicleConsulting);
        var existing = Operation.Create(request.ClientId, request.IdempotencyKey, request.Type, 12.50m).Value!;
        h.Operations.Seed(existing);

        var result = await h.Service.CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Same(existing, result.Value);
        Assert.Empty(h.Clients.Debits);
        Assert.Empty(h.Operations.Added);
        Assert.Empty(h.Transactions.Added);
        Assert.Empty(h.UnitOfWork.Calls);
        Assert.Equal(0, h.Provider.Calls);
    }

    [Fact]
    public async Task CreateAsync_WithExistingKeyAndDifferentType_Fails()
    {
        var h = new Harness();
        var request = Request(OperationType.DocumentIssuance);
        h.Operations.Seed(Operation.Create(request.ClientId, request.IdempotencyKey, OperationType.VehicleConsulting, 12.50m).Value!);

        var result = await h.Service.CreateAsync(request);

        Assert.True(result.IsFailure);
        Assert.Contains("chave de idempotência", result.Error);
        Assert.Empty(h.Clients.Debits);
    }

    [Fact]
    public async Task CreateAsync_WhenSaveThrowsDuplicate_RollsBackAndReturnsExisting()
    {
        var h = new Harness();
        var request = Request();
        var winner = Operation.Create(request.ClientId, request.IdempotencyKey, request.Type, 12.50m).Value!;
        h.Operations.VisibleAfterFirstLookup = winner;
        h.UnitOfWork.SaveException = new DuplicateOperationException();

        var result = await h.Service.CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Same(winner, result.Value);
        Assert.Equal(1, h.UnitOfWork.Count("Rollback"));
        Assert.Equal(0, h.UnitOfWork.Count("Commit"));
        Assert.Equal(0, h.Provider.Calls);
    }

    [Fact]
    public async Task CreateAsync_WhenSaveThrowsDuplicateButNothingFound_Rethrows()
    {
        var h = new Harness();
        h.UnitOfWork.SaveException = new DuplicateOperationException();

        await Assert.ThrowsAsync<DuplicateOperationException>(() => h.Service.CreateAsync(Request()));

        Assert.Equal(1, h.UnitOfWork.Count("Rollback"));
    }

    [Fact]
    public async Task CreateAsync_WhenSaveThrowsUnexpectedError_RollsBackAndRethrows()
    {
        var h = new Harness();
        h.UnitOfWork.SaveException = new InvalidOperationException("db down");

        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Service.CreateAsync(Request()));

        Assert.Equal(1, h.UnitOfWork.Count("Rollback"));
        Assert.Equal(0, h.UnitOfWork.Count("Commit"));
        Assert.Equal(0, h.Provider.Calls);
    }
}

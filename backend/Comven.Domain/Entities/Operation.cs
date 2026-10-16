using Comven.Domain.Common;
using Comven.Domain.Enums;

namespace Comven.Domain.Entities;

public class Operation
{
    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public OperationType Type { get; private set; }
    public decimal Amount { get; private set; }
    public OperationStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    
    private Operation() { }
    private Operation(Guid clientId, Guid idempotencyKey, OperationType type, decimal amount)
    {
        Id = Guid.NewGuid();
        ClientId = clientId;
        IdempotencyKey = idempotencyKey;
        Type = type;
        Amount = amount;
        Status = OperationStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public static DomainResult<Operation> Create(Guid clientId, Guid idempotencyKey, OperationType type, decimal amount)
    {
        if (clientId == Guid.Empty)
            return DomainResult<Operation>.Failure("Cliente inválido.");

        if (idempotencyKey == Guid.Empty)
            return DomainResult<Operation>.Failure("Chave de idempotência inválida.");

        if (amount <= 0)
            return DomainResult<Operation>.Failure("Valor da operação inválido.");

        return DomainResult<Operation>.Success(new Operation(
            clientId,
            idempotencyKey,
            type,
            amount
        ));
    }

    public DomainResult Complete()
    {
        if (Status != OperationStatus.Pending)
            return DomainResult.Failure("A operação já foi finalizada.");

        Status = OperationStatus.Completed;

        FinishedAt = DateTime.UtcNow;

        return DomainResult.Success();
    }
}

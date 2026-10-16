using Comven.Domain.Common;
using Comven.Domain.Enums;

namespace Comven.Domain.Entities;

public class FinancialTransaction
{
    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid OperationId { get; private set; }
    public FinancialTransactionType Type { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private FinancialTransaction() { }

    private FinancialTransaction(Guid clientId, Guid operationId, FinancialTransactionType type, decimal amount)
    {
        Id = Guid.NewGuid();
        ClientId = clientId;
        OperationId = operationId;
        Type = type;
        Amount = amount;
        CreatedAt = DateTime.UtcNow;
    }

    public static DomainResult<FinancialTransaction> CreateDebit(
        Guid clientId,
        Guid operationId,
        decimal amount)
    {
        return Create(
            clientId,
            operationId,
            FinancialTransactionType.Debit,
            amount);
    }

    public static DomainResult<FinancialTransaction> CreateRefund( Guid clientId, Guid operationId, decimal amount)
    {
        return Create(
            clientId,
            operationId,
            FinancialTransactionType.Refund,
            amount);
    }

    private static DomainResult<FinancialTransaction> Create( Guid clientId, Guid operationId, FinancialTransactionType type, decimal amount)
    {
        if (clientId == Guid.Empty)
            return DomainResult<FinancialTransaction>.Failure(
                "Cliente inválido.");

        if (operationId == Guid.Empty)
            return DomainResult<FinancialTransaction>.Failure(
                "Operação inválida.");

        if (amount <= 0)
            return DomainResult<FinancialTransaction>.Failure(
                "O valor da movimentação deve ser positivo.");

        return DomainResult<FinancialTransaction>.Success(
            new FinancialTransaction(
                clientId,
                operationId,
                type,
                amount
        ));
    }
}

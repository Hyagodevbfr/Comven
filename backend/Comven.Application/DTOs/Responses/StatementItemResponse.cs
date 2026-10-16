using Comven.Domain.Enums;

namespace Comven.Application.DTOs.Responses;

public record StatementItemResponse(Guid Id, Guid OperationId, OperationType OperationType, OperationStatus OperationStatus, FinancialTransactionType TransactionType, decimal Amount, DateTime CreatedAt
);
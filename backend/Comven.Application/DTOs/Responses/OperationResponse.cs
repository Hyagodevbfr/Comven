
using Comven.Domain.Enums;

namespace Comven.Application.DTOs.Responses;

public record OperationResponse(Guid Id, Guid ClientId, OperationType Type, decimal Amount, OperationStatus Status, DateTime CreatedAt, DateTime? FinishedAt);
using Comven.Domain.Enums;

namespace Comven.Application.DTOs.Requests;

public record CreateOperationRequest(Guid ClientId, Guid IdempotencyKey, OperationType Type);
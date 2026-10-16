using Comven.Domain.Entities;

namespace Comven.Application.Interfaces.Repositories;

public interface IOperationRepository
{
    Task<Operation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Operation?> GetByIdempotencyKeyAsync(Guid clientId, Guid idempotencyKey, CancellationToken cancellationToken = default);
    Task AddAsync(Operation operation, CancellationToken cancellationToken = default);
    Task<bool> TryMarkAsFailedAsync(Guid operationId, CancellationToken cancellationToken = default);

}

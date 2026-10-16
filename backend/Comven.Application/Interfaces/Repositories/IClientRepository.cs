using Comven.Domain.Entities;

namespace Comven.Application.Interfaces.Repositories;

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task AddAsync(Client client, CancellationToken cancellationToken = default);
    Task<bool> TryDebitAsync(Guid clientId, decimal amount, CancellationToken cancellationToken = default);
    Task<bool> TryRefundAsync(Guid clientId, decimal amount, CancellationToken cancellationToken = default);
}

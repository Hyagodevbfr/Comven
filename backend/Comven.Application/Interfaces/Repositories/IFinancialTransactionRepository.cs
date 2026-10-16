using Comven.Domain.Entities;
using Comven.Domain.Enums;

namespace Comven.Application.Interfaces.Repositories;

public interface IFinancialTransactionRepository
{
    Task AddAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid operationId, FinancialTransactionType type, CancellationToken cancellationToken = default);
}

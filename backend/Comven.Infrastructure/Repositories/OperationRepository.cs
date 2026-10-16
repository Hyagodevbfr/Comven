using Comven.Application.Interfaces.Repositories;
using Comven.Domain.Entities;
using Comven.Domain.Enums;
using Comven.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comven.Infrastructure.Repositories;

public class OperationRepository : IOperationRepository
{
    private readonly AppDbContext _context;

    public OperationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Operation?> GetByIdAsync( Guid operationId, CancellationToken cancellationToken = default)
    {
        return await _context.Operations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == operationId,
                cancellationToken
        );
    }

    public async Task<Operation?> GetByIdempotencyKeyAsync(Guid clientId, Guid idempotencyKey, CancellationToken cancellationToken = default)
    {
        return await _context.Operations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.ClientId == clientId && o.IdempotencyKey == idempotencyKey, cancellationToken);
    }
   
    public async Task AddAsync(Operation operation, CancellationToken cancellationToken = default)
    {
        await _context.Operations.AddAsync(operation, cancellationToken);
    }

    public async Task<bool> TryMarkAsFailedAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        var finishedAt = DateTime.UtcNow;

        var affectedRows = await _context.Operations.Where(x => x.Id == operationId && x.Status == OperationStatus.Pending)
           .ExecuteUpdateAsync(
               setters => setters
                   .SetProperty(
                       x => x.Status,
                       OperationStatus.Failed)
                   .SetProperty(
                       x => x.FinishedAt,
                       (DateTime?)finishedAt),
               cancellationToken);

        return affectedRows == 1;
    }

    public async Task<bool> TryMarkAsCompletedAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        var finishedAt = DateTime.UtcNow;

        var affectedRows = await _context.Operations
            .Where(x => x.Id == operationId &&
                        x.Status == OperationStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.Status,
                        OperationStatus.Completed)
                    .SetProperty(
                        x => x.FinishedAt,
                        (DateTime?)finishedAt),
                cancellationToken);

        return affectedRows == 1;
    }
}

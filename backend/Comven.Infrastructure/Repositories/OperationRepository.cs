using Comven.Application.Interfaces.Repositories;
using Comven.Domain.Entities;
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

    public async Task<Operation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Operations.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
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

    public Task<bool> TryMarkAsFailedAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

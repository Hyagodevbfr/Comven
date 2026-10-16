using Comven.Application.DTOs.Responses;
using Comven.Application.Interfaces.Repositories;
using Comven.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comven.Infrastructure.Repositories;

public class StatementRepository : IStatementRepository
{
    private readonly AppDbContext _context;

    public StatementRepository(AppDbContext context)
    {
        _context = context;
    }

    
    public async Task<StatementResponse> GetByPeriodAsync(Guid clientId, DateTime startUtc, DateTime endExclusiveUtc, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query =
            from transaction in _context.FinancialTransactions.AsNoTracking()
            join operation in _context.Operations.AsNoTracking()
                on transaction.OperationId equals operation.Id
            where transaction.ClientId == clientId
            && operation.ClientId == clientId
            && transaction.CreatedAt >= startUtc
            && transaction.CreatedAt < endExclusiveUtc
            select new
            {
                Transaction = transaction,
                Operation = operation
            };

        var totalItems = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.Transaction.CreatedAt)
            .ThenByDescending(x => x.Transaction.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new StatementItemResponse(
                x.Transaction.Id,
                x.Operation.Id,
                x.Operation.Type,
                x.Operation.Status,
                x.Transaction.Type,
                x.Transaction.Amount,
                x.Transaction.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return new StatementResponse(
            items,
            totalItems,
            page,
            pageSize);
    }

    
}
using Comven.Application.Interfaces.Repositories;
using Comven.Domain.Entities;
using Comven.Domain.Enums;
using Comven.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comven.Infrastructure.Repositories
{
    public class FinancialTransactionRepository : IFinancialTransactionRepository
    {
        private readonly AppDbContext _context;

        public FinancialTransactionRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task AddAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default)
        {
            await _context.FinancialTransactions.AddAsync(transaction, cancellationToken);
        }

        public async Task<bool> ExistsAsync(Guid operationId, FinancialTransactionType type, CancellationToken cancellationToken = default)
        {
            return await _context.FinancialTransactions.AnyAsync(ft => ft.OperationId == operationId && ft.Type == type, cancellationToken);
        }
    }
}

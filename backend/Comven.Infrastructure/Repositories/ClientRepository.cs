using Comven.Application.Interfaces.Repositories;
using Comven.Domain.Entities;
using Comven.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comven.Infrastructure.Repositories
{
    public class ClientRepository : IClientRepository
    {
        private readonly AppDbContext _context;

        public ClientRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }

        public async Task AddAsync(Client client, CancellationToken cancellationToken = default)
        {
            await _context.Clients.AddAsync(client, cancellationToken);
        }


        public async Task<bool> TryDebitAsync(Guid clientId, decimal amount, CancellationToken cancellationToken = default)
        {
            if (amount <= 0)
                return false;

            var affectedRows = await _context.Clients.Where(c => c.Id == clientId && c.Balance >= amount)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(c => c.Balance, c => c.Balance - amount), cancellationToken);

            return affectedRows == 1;
        }

        public async Task<bool> TryRefundAsync(Guid clientId, decimal amount, CancellationToken cancellationToken = default)
        {
            if (amount <= 0)
                return false;

            var affectedRows = await _context.Clients.Where(c => c.Id == clientId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(c => c.Balance, c => c.Balance + amount), cancellationToken);

            return affectedRows == 1;
        }
    }
}

using Comven.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;
using Comven.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Comven.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork, IAsyncDisposable
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
            throw new InvalidOperationException("Já existe uma transação em andamento.");

        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException pgEx &&
                  pgEx.SqlState == PostgresErrorCodes.UniqueViolation &&
                  pgEx.ConstraintName ==
                      "IX_Operations_ClientId_IdempotencyKey")
        {
            _context.ChangeTracker.Clear();

            throw new DuplicateOperationException();
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
            throw new InvalidOperationException("Não há transação em andamento.");

        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();

        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
            return;

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();

        _transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is null)
            return;

        await _transaction.DisposeAsync();
        _transaction = null;
    }

}

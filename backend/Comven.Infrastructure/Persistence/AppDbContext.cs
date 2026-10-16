using Comven.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Comven.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Operation> Operations => Set<Operation>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

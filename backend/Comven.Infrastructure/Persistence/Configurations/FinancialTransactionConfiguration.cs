using Comven.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comven.Infrastructure.Persistence.Configurations;

public class FinancialTransactionConfiguration : IEntityTypeConfiguration<FinancialTransaction>
{
    public void Configure(EntityTypeBuilder<FinancialTransaction> builder)
    {
        builder.ToTable("FinancialTransactions", table =>
        {
            table.HasCheckConstraint("CK_FinancialTransactions_Amount", "\"Amount\" > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Operation>()
            .WithMany()
            .HasForeignKey(x => x.OperationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new
        {
            x.OperationId,
            x.Type
        }).IsUnique();

        builder.HasIndex(x => new
        {
            x.ClientId,
            x.CreatedAt
        });
    }
}

using Comven.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comven.Infrastructure.Persistence.Configurations;

public class OperationConfiguration : IEntityTypeConfiguration<Operation>
{
    public void Configure(EntityTypeBuilder<Operation> builder)
    {
        builder.ToTable("Operations", table =>
        {
            table.HasCheckConstraint("CK_Operations_Amount", "\"Amount\" >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id);

        builder.Property(x => x.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.IdempotencyKey)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.FinishedAt)
            .HasColumnType("timestamp with time zone");

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new
        {
            x.ClientId,
            x.IdempotencyKey
        });

        builder.HasIndex(x => new
        {
            x.ClientId,
            x.CreatedAt
        });

    }
}

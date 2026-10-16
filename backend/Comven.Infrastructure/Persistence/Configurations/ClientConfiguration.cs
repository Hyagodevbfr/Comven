using Comven.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Comven.Infrastructure.Persistence.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients", table =>
        {
            table.HasCheckConstraint(
                "CK_Clients_Balance",
                "\"Balance\" >= 0");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.Balance)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }
}

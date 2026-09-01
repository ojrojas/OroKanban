using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildingBlocks.Kernel.Domain.Persistence;

public sealed class OutboxEntityTypeConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.OccurredOn).IsRequired();
        builder.HasIndex(x => x.ProcessedOn);
    }
}

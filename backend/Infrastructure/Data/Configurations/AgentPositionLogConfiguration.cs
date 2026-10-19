using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Infrastructure.Data.Configurations;

public class AgentPositionLogConfiguration : IEntityTypeConfiguration<AgentPositionLog>
{
    public void Configure(EntityTypeBuilder<AgentPositionLog> b)
    {
        b.ToTable("AgentPositionLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Neighborhood).IsRequired().HasMaxLength(120);
        b.HasIndex(x => new { x.AgentId, x.RecordedAt });
        // Historico de posicao some junto com o agente (LGPD).
        b.HasOne(x => x.Agent).WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
    }
}

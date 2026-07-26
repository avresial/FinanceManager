using FinanceManager.Domain.Administration.Monitoring;
using FinanceManager.Domain.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Persistence.Configurations;

public class NewVisitsConfiguration : IEntityTypeConfiguration<NewVisits>
{
    public void Configure(EntityTypeBuilder<NewVisits> builder)
    {
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();
    }
}
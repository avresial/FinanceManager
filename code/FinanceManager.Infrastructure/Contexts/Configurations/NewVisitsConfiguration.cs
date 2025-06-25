using FinanceManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class NewVisitsConfiguration : IEntityTypeConfiguration<NewVisits>
{
    public void Configure(EntityTypeBuilder<NewVisits> builder)
    {
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();
    }
}
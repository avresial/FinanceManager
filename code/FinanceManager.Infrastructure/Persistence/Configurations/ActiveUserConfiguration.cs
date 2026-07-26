using FinanceManager.Domain.Administration.Monitoring;
using FinanceManager.Domain.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Persistence.Configurations;

internal class ActiveUserConfiguration : IEntityTypeConfiguration<ActiveUser>
{
    public void Configure(EntityTypeBuilder<ActiveUser> builder)
    {
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();
    }

}
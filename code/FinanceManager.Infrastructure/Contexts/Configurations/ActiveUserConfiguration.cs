using FinanceManager.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;
internal class ActiveUserConfiguration : IEntityTypeConfiguration<ActiveUser>
{
    public void Configure(EntityTypeBuilder<ActiveUser> builder)
    {
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();
    }

}

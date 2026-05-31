using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class UserDtoConfiguration : IEntityTypeConfiguration<UserDto>
{
    public void Configure(EntityTypeBuilder<UserDto> builder)
    {
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        // Logins are the account identifier (the email); a unique index closes the check-then-insert race in
        // registration where two concurrent requests could otherwise both create the same login.
        builder.HasIndex(e => e.Login)
            .IsUnique();
    }
}
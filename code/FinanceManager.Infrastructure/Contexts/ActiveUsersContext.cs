using FinanceManager.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Contexts;

public class ActiveUsersContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<ActiveUser>()
                    .Property(f => f.Id)
                    .ValueGeneratedOnAdd();

        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseInMemoryDatabase(databaseName: "ActiveUsersDb");
    }

    public DbSet<ActiveUser> ActiveUsers { get; set; }
}

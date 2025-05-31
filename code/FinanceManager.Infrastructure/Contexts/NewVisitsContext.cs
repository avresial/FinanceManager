using FinanceManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Contexts;

public class NewVisitsContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NewVisits>()
                    .Property(f => f.Id)
                    .ValueGeneratedOnAdd();

        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseInMemoryDatabase(databaseName: "NewVisitsDb");
    }

    public DbSet<NewVisits> NewVisits { get; set; }
}


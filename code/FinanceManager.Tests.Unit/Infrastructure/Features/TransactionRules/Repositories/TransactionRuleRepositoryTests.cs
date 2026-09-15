using FinanceManager.Domain.Identity.Dtos;
using FinanceManager.Domain.TransactionRules.Entities;
using FinanceManager.Infrastructure.Features.TransactionRules.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.TransactionRules.Repositories;

[Trait("Category", "Unit")]
public sealed class TransactionRuleRepositoryTests : IDisposable
{
    private readonly AppDbContext _context = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    [Fact]
    public async Task GetByUserId_ReturnsRulesInOrder()
    {
        _context.Users.Add(new UserDto
        {
            Id = 1,
            Login = "rules-user",
            Password = "password",
            CreationDate = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.TransactionRules.AddRange(
            new TransactionRuleDefinition { UserId = 1, Order = 2, Name = "second" },
            new TransactionRuleDefinition { UserId = 1, Order = 1, Name = "first" });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var rules = await new TransactionRuleRepository(_context).GetByUserId(1, TestContext.Current.CancellationToken);

        Assert.Equal(["first", "second"], rules.Select(rule => rule.Name));
    }

    [Fact]
    public async Task Reorder_UsesDenseOrderWithoutUniqueIndexCollisions()
    {
        _context.Users.Add(new UserDto
        {
            Id = 1,
            Login = "rules-user",
            Password = "password",
            CreationDate = DateTime.UtcNow
        });
        var first = new TransactionRuleDefinition { UserId = 1, Order = 1, Name = "first" };
        var second = new TransactionRuleDefinition { UserId = 1, Order = 2, Name = "second" };
        _context.TransactionRules.AddRange(first, second);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new TransactionRuleRepository(_context);
        Assert.True(await repository.Reorder(1, [second.Id, first.Id], TestContext.Current.CancellationToken));

        var rules = await repository.GetByUserId(1, TestContext.Current.CancellationToken);
        Assert.Equal([second.Id, first.Id], rules.Select(rule => rule.Id));
        Assert.Equal([1, 2], rules.Select(rule => rule.Order));
    }

    public void Dispose() => _context.Dispose();
}
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Tests.Unit.Components.Features.Identity;

[Trait("Category", "Unit")]
public sealed class UserDisplayNameTests
{
    [Theory]
    [InlineData("Demo", "guest--1", "Demo")]
    [InlineData("  Demo ", "guest--1", "Demo")]
    [InlineData(null, "guest--1", "guest--1")]
    [InlineData(" ", "guest--1", "guest--1")]
    public void From_PrefersFirstNameAndFallsBackToLogin(string? firstName, string login, string expected)
    {
        var user = new User { Login = login, FirstName = firstName, CreationDate = DateTime.UtcNow };

        Assert.Equal(expected, UserDisplayName.From(user));
    }
}
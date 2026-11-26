using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Contracts.Transactions.Cash;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.MoneyFlow.Controllers;

[Trait("Category", "Integration")]
public class UnconvertedCurrenciesControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const int _owner = 1132;
    private const int _pln = 0;
    private const int _eur = 2;
    private TestDatabase? _database;

    protected override bool ReuseApplicationHost => false;

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<DbContextOptions<AppDbContext>>();
        _database = new TestDatabase();
        services.AddSingleton(_database.Context);
        var planVerifier = new Mock<IUserPlanVerifier>();
        planVerifier.Setup(x => x.CanAddMoreEntries(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
        services.AddSingleton(planVerifier.Object);
    }

    [Fact]
    public async Task Get_NamesTheCurrenciesOtherThanTheDisplayCurrencyThatTheOwnerHolds()
    {
        var ct = TestContext.Current.CancellationToken;
        _database!.Context.Currencies.AddRange(new Currency(_pln, "PLN", "zł"), new Currency(_eur, "EUR", "€"));
        await _database.Context.SaveChangesAsync(ct);
        Authorize("owner", _owner, UserRole.User);
        var client = new UnconvertedCurrenciesHttpClient(Client);
        var day = DateTime.UtcNow.Date.AddDays(-2);

        Assert.Empty(await client.GetCodes(_owner, _pln));

        using var plnEntry = await Client.PostAsJsonAsync("api/transactions/cash", new CreateCashEntryRequest(_pln, day, 100m, "pln", null), ct);
        using var eurEntry = await Client.PostAsJsonAsync("api/transactions/cash", new CreateCashEntryRequest(_eur, day, -40m, "eur", null), ct);
        Assert.Equal(HttpStatusCode.OK, plnEntry.StatusCode);
        Assert.Equal(HttpStatusCode.OK, eurEntry.StatusCode);

        // A new client skips the first one's shared answer; seen from the euro, the zloty is the foreign currency.
        Assert.Equal(["EUR"], await new UnconvertedCurrenciesHttpClient(Client).GetCodes(_owner, _pln));
        Assert.Equal(["PLN"], await new UnconvertedCurrenciesHttpClient(Client).GetCodes(_owner, _eur));
    }

    [Fact]
    public async Task Get_RejectsAnotherUser()
    {
        Authorize("owner", _owner, UserRole.User);

        using var response = await Client.GetAsync($"api/UnconvertedCurrencies/{_owner + 1}/{_pln}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    public override void Dispose()
    {
        base.Dispose();
        _database?.Dispose();
        GC.SuppressFinalize(this);
    }
}
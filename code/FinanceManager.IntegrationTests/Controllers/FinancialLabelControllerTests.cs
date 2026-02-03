using FinanceManager.Application.Commands.Account;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinanceManager.IntegrationTests.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class FinancialLabelControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private TestDatabase? _testDatabase;

    protected override void ConfigureServices(IServiceCollection services)
    {
        _testDatabase = new TestDatabase();

        // remove any registration for AppDbContext
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        services.AddSingleton(_testDatabase!.Context);
    }

    private async Task SeedWithTestFinancialLabel(string name = "Test Label")
    {
        if (await _testDatabase!.Context.FinancialLabels.AnyAsync(x => x.Name == name))
            return;

        _testDatabase!.Context.FinancialLabels.Add(new FinancialLabel { Name = name });
        await _testDatabase.Context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetCount_ReturnsCount()
    {
        await SeedWithTestFinancialLabel();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new FinancialLabelHttpClient(Client).GetCount(TestContext.Current.CancellationToken);

        Assert.True(result > 0);
    }

    [Fact]
    public async Task Get_ReturnsLabel()
    {
        await SeedWithTestFinancialLabel();
        Authorize("TestUser", 1, UserRole.User);

        var labels = await new FinancialLabelHttpClient(Client).Get(0, 10, TestContext.Current.CancellationToken);
        var label = labels.FirstOrDefault();

        Assert.NotNull(label);
        var result = await new FinancialLabelHttpClient(Client).Get(label.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(label.Id, result.Id);
    }

    [Fact]
    public async Task GetByIndexAndCount_ReturnsList()
    {
        await SeedWithTestFinancialLabel();
        Authorize("TestUser", 1, UserRole.User);

        var result = await new FinancialLabelHttpClient(Client).Get(0, 10, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task Add_AddsLabel()
    {
        Authorize("TestUser", 1, UserRole.User);

        var addResult = await new FinancialLabelHttpClient(Client).Add(new AddFinancialLabel("New Label"), TestContext.Current.CancellationToken);
        var existing = await new FinancialLabelHttpClient(Client).Get(0, 10, TestContext.Current.CancellationToken);

        Assert.True(addResult);
        Assert.Contains(existing, l => l.Name == "New Label");
    }

    [Fact]
    public async Task UpdateName_UpdatesLabel()
    {
        Authorize("TestUser", 1, UserRole.User);

        await new FinancialLabelHttpClient(Client).Add(new AddFinancialLabel("New Label"), TestContext.Current.CancellationToken);

        var labels = await new FinancialLabelHttpClient(Client).Get(0, 10, TestContext.Current.CancellationToken);
        var label = labels.FirstOrDefault();

        Assert.NotNull(label);
        var updateResult = await new FinancialLabelHttpClient(Client).UpdateName(label.Id, "Updated Label", TestContext.Current.CancellationToken);

        Assert.True(updateResult);

        var updatedlabels = await new FinancialLabelHttpClient(Client).Get(0, 10, TestContext.Current.CancellationToken);
        Assert.Equal("Updated Label", updatedlabels.First().Name);
    }

    [Fact]
    public async Task Delete_DeletesLabel()
    {
        Authorize("TestUser", 1, UserRole.User);

        await new FinancialLabelHttpClient(Client).Add(new AddFinancialLabel("New Label"), TestContext.Current.CancellationToken);
        var labels = await new FinancialLabelHttpClient(Client).Get(0, 10, TestContext.Current.CancellationToken);
        var label = labels.FirstOrDefault();

        Assert.NotNull(label);
        var deleteResult = await new FinancialLabelHttpClient(Client).Delete(label.Id, TestContext.Current.CancellationToken);

        Assert.True(deleteResult);

        var finalLabels = await new FinancialLabelHttpClient(Client).Get(0, 10, TestContext.Current.CancellationToken);
        Assert.Empty(finalLabels);
    }

    [Fact]
    public async Task GetByAccountId_ReturnsLabel()
    {
        Authorize("TestUser", 1, UserRole.User);
        var addLabel = new AddFinancialLabel("Test Label");
        await new FinancialLabelHttpClient(Client).Add(addLabel, TestContext.Current.CancellationToken);
        var label = await _testDatabase!.Context.FinancialLabels.FirstAsync(x => x.Name == addLabel.Name, TestContext.Current.CancellationToken);
        _testDatabase!.Context.CurrencyEntries.Add(new CurrencyAccountEntry(1, 0, DateTime.UtcNow, 1, 1)
        {
            Labels = [label]
        });
        await _testDatabase!.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        // Add a label to verify it exists

        // GetByAccountId currently returns empty list because FinancialLabel.Entries collection was removed
        var result = await new FinancialLabelHttpClient(Client).GetByAccountId(1, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Single(result);
    }

    public override void Dispose()
    {
        base.Dispose();
        if (_testDatabase is null)
            return;

        _testDatabase.Dispose();
        _testDatabase = null;
        GC.SuppressFinalize(this);
    }
}
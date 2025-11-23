using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinanceManager.IntegrationTests.Controllers;

[Collection("api")]
public class BondDetailsControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private TestDatabase? _testDatabase;

    protected override void ConfigureServices(IServiceCollection services)
    {
        _testDatabase = new TestDatabase();

        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        services.AddSingleton(_testDatabase!.Context);
    }

    [Fact]
    public async Task Add_CreatesBondDetails()
    {
        // Arrange
        Authorize("TestUser", 1, UserRole.Admin);
        var client = new BondDetailsHttpClient(Client);
        BondDetails bond = new("Test Bond", "Test Issuer", new DateOnly(2024, 1, 1), new DateOnly(2028, 1, 1),
            new List<BondCalculationMethod>
            {
                new BondCalculationMethod
                {
                    DateOperator = DateOperator.UntilDate,
                    DateValue = "2028-01-01",
                    Rate = 5.0m
                }
            });

        // Act
        var result = await client.Add(bond, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Bond", result.Name);
        Assert.Equal("Test Issuer", result.Issuer);
        Assert.True(result.Id > 0);
    }

    [Fact]
    public async Task GetById_ReturnsExistingBond()
    {
        // Arrange
        Authorize("TestUser", 1, UserRole.Admin);
        var client = new BondDetailsHttpClient(Client);
        var bond = new BondDetails
        {
            Name = "Get Test Bond",
            Issuer = "Get Issuer",
            StartEmissionDate = new DateOnly(2024, 1, 1),
            EndEmissionDate = new DateOnly(2028, 1, 1),
            Type = BondType.InflationBond
        };
        var added = await client.Add(bond, TestContext.Current.CancellationToken);

        // Act
        var result = await client.GetById(added!.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(added.Id, result.Id);
        Assert.Equal("Get Test Bond", result.Name);
        Assert.Equal("Get Issuer", result.Issuer);
    }

    [Fact]
    public async Task GetAll_ReturnsAllBonds()
    {
        // Arrange
        Authorize("TestUser", 1, UserRole.Admin);
        var client = new BondDetailsHttpClient(Client);
        await client.Add(new BondDetails
        {
            Name = "Bond 1",
            Issuer = "Issuer A",
            StartEmissionDate = new DateOnly(2024, 1, 1),
            EndEmissionDate = new DateOnly(2028, 1, 1),
            Type = BondType.InflationBond
        }, TestContext.Current.CancellationToken);
        await client.Add(new BondDetails
        {
            Name = "Bond 2",
            Issuer = "Issuer B",
            StartEmissionDate = new DateOnly(2024, 6, 1),
            EndEmissionDate = new DateOnly(2028, 6, 1),
            Type = BondType.InflationBond
        }, TestContext.Current.CancellationToken);

        // Act
        var result = await client.GetAll(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 2);
        Assert.Contains(result, b => b.Name == "Bond 1");
        Assert.Contains(result, b => b.Name == "Bond 2");
    }

    [Fact]
    public async Task GetByIssuer_ReturnsFilteredBonds()
    {
        // Arrange
        Authorize("TestUser", 1, UserRole.Admin);
        var client = new BondDetailsHttpClient(Client);
        await client.Add(new BondDetails
        {
            Name = "Treasury Bond 1",
            Issuer = "Ministry of Finance",
            StartEmissionDate = new DateOnly(2024, 1, 1),
            EndEmissionDate = new DateOnly(2028, 1, 1),
            Type = BondType.InflationBond
        }, TestContext.Current.CancellationToken);
        await client.Add(new BondDetails
        {
            Name = "Treasury Bond 2",
            Issuer = "Ministry of Finance",
            StartEmissionDate = new DateOnly(2024, 3, 1),
            EndEmissionDate = new DateOnly(2028, 3, 1),
            Type = BondType.InflationBond
        }, TestContext.Current.CancellationToken);
        await client.Add(new BondDetails
        {
            Name = "Corporate Bond",
            Issuer = "Private Company",
            StartEmissionDate = new DateOnly(2024, 1, 1),
            EndEmissionDate = new DateOnly(2027, 1, 1),
            Type = BondType.InflationBond
        }, TestContext.Current.CancellationToken);

        // Act
        var result = await client.GetByIssuer("Ministry of Finance", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, b => Assert.Equal("Ministry of Finance", b.Issuer));
    }

    [Fact]
    public async Task Update_ModifiesBondDetails()
    {
        // Arrange
        Authorize("TestUser", 1, UserRole.Admin);
        var client = new BondDetailsHttpClient(Client);
        var bond = new BondDetails
        {
            Name = "Original Name",
            Issuer = "Original Issuer",
            StartEmissionDate = new DateOnly(2024, 1, 1),
            EndEmissionDate = new DateOnly(2028, 1, 1),
            Type = BondType.InflationBond
        };
        var added = await client.Add(bond, TestContext.Current.CancellationToken);

        // Act
        var updated = added! with { Name = "Updated Name", Issuer = "Updated Issuer" };
        var updateResult = await client.Update(added.Id, updated, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(updateResult);
        var retrieved = await client.GetById(added.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(retrieved);
        Assert.Equal("Updated Name", retrieved.Name);
        Assert.Equal("Updated Issuer", retrieved.Issuer);
    }

    [Fact]
    public async Task Delete_RemovesBondDetails()
    {
        // Arrange
        Authorize("TestUser", 1, UserRole.Admin);
        var client = new BondDetailsHttpClient(Client);
        var bond = new BondDetails
        {
            Name = "To Delete",
            Issuer = "Delete Issuer",
            StartEmissionDate = new DateOnly(2024, 1, 1),
            EndEmissionDate = new DateOnly(2028, 1, 1),
            Type = BondType.InflationBond
        };
        var added = await client.Add(bond, TestContext.Current.CancellationToken);

        // Act
        var deleteResult = await client.Delete(added!.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(deleteResult);
        var allBonds = await client.GetAll(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(allBonds, b => b.Id == added.Id);
    }

    public void Dispose()
    {
        if (_testDatabase is null)
            return;

        _testDatabase.Dispose();
        _testDatabase = null;
    }
}

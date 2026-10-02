using FinanceManager.Domain.Identity.Dtos;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.GiftCodes;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Identity.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public sealed class GiftCodesControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private const int _userId = 420;
    private TestDatabase? _database;

    protected override void ConfigureServices(IServiceCollection services)
    {
        var descriptor = services.SingleOrDefault(service => service.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor is not null)
            services.Remove(descriptor);

        _database = new TestDatabase();
        services.AddSingleton(_database.Context);
    }

    [Fact]
    public async Task UserCanRedeemCodeOnceAndCodeCannotBeReused()
    {
        await SeedUser(PricingLevel.Free);
        Authorize("admin", 17, UserRole.Admin);
        var generated = await Generate(PricingLevel.Basic);
        ClearAuthorization();
        var anonymous = await Client.PostAsJsonAsync(
            "api/gift-codes/redeem", new RedeemGiftCode(generated.Code), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Authorize("user", _userId, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        var first = await Client.PostAsJsonAsync("api/gift-codes/redeem", new RedeemGiftCode(generated.Code), ct);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        var replay = await Client.PostAsJsonAsync("api/gift-codes/redeem", new RedeemGiftCode(generated.Code), ct);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);

        Assert.Equal(PricingLevel.Basic, (await _database!.Context.Users.AsNoTracking()
            .SingleAsync(user => user.Id == _userId, ct)).PricingLevel);
        var savedCode = await _database.Context.GiftCodes.AsNoTracking().SingleAsync(ct);
        Assert.Equal(GiftCodeState.Redeemed, savedCode.State);
        Assert.Equal(_userId, savedCode.RedeemedByUserId);
    }

    [Fact]
    public async Task Redeem_UsesAuthenticatedUserIdAndIgnoresSubmittedUserId()
    {
        await SeedUser(PricingLevel.Free);
        await SeedUser(PricingLevel.Free, _userId + 1);
        Authorize("admin", 17, UserRole.Admin);
        var generated = await Generate(PricingLevel.Basic);
        Authorize("user", _userId, UserRole.User);

        var response = await Client.PostAsJsonAsync("api/gift-codes/redeem",
            new { code = generated.Code, userId = _userId + 1 }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(PricingLevel.Basic, (await _database!.Context.Users.AsNoTracking()
            .SingleAsync(user => user.Id == _userId, TestContext.Current.CancellationToken)).PricingLevel);
        Assert.Equal(PricingLevel.Free, (await _database.Context.Users.AsNoTracking()
            .SingleAsync(user => user.Id == _userId + 1, TestContext.Current.CancellationToken)).PricingLevel);
    }

    [Fact]
    public async Task Redeem_MalformedAndUnknownCodesReturnBadRequest()
    {
        await SeedUser(PricingLevel.Free);
        Authorize("user", _userId, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        using var malformed = await Client.PostAsJsonAsync(
            "api/gift-codes/redeem", new RedeemGiftCode("not-a-code"), ct);
        using var unknown = await Client.PostAsJsonAsync(
            "api/gift-codes/redeem", new RedeemGiftCode("0000-0000-0000-0000-0000-0000-0000-0000"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(PricingLevel.Free, (await _database!.Context.Users.AsNoTracking()
            .SingleAsync(user => user.Id == _userId, ct)).PricingLevel);
    }

    [Fact]
    public async Task InvalidTierAndNonUpgradeAreRejectedWithoutConsumingCode()
    {
        await SeedUser(PricingLevel.Premium);
        Authorize("admin", 17, UserRole.Admin);
        var invalidTier = await Client.PostAsJsonAsync(
            "api/admin/gift-codes", new GenerateGiftCode(PricingLevel.Free), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidTier.StatusCode);

        var generated = await Generate(PricingLevel.Basic);
        Authorize("user", _userId, UserRole.User);
        var notUpgrade = await Client.PostAsJsonAsync(
            "api/gift-codes/redeem", new RedeemGiftCode(generated.Code), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, notUpgrade.StatusCode);
        Assert.Equal(GiftCodeState.Active, (await _database!.Context.GiftCodes.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken)).State);
        Assert.Equal(PricingLevel.Premium, (await _database.Context.Users.AsNoTracking()
            .SingleAsync(user => user.Id == _userId, TestContext.Current.CancellationToken)).PricingLevel);
    }

    private async Task<GeneratedGiftCode> Generate(PricingLevel level)
    {
        var response = await Client.PostAsJsonAsync(
            "api/admin/gift-codes", new GenerateGiftCode(level), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GeneratedGiftCode>(TestContext.Current.CancellationToken))!;
    }

    private async Task SeedUser(PricingLevel level, int userId = _userId)
    {
        _database!.Context.Users.Add(new UserDto
        {
            Id = userId,
            Login = $"gift-user-{userId}@example.com",
            Password = "test",
            PricingLevel = level,
            UserRole = UserRole.User,
            CreationDate = DateTime.UtcNow
        });
        await _database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public override void Dispose()
    {
        base.Dispose();
        _database?.Dispose();
        _database = null;
    }
}
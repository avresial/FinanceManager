using FinanceManager.Domain.Identity.Dtos;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.GiftCodes;
using FinanceManager.Infrastructure.Features.Identity.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

[Trait("Category", "Integration")]
public sealed class SqliteGiftCodeRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _context;

    public SqliteGiftCodeRepositoryTests()
    {
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task Redeem_ClaimsCodeAndUpgradesUserInOneOperation()
    {
        var ct = TestContext.Current.CancellationToken;
        await AddUser(10, PricingLevel.Free, ct);
        var giftCode = await AddGiftCode("C001", PricingLevel.Basic, ct);

        var result = await new GiftCodeRepository(_context).Redeem(giftCode.CodeHash, 10, UtcNow, ct);

        Assert.Equal(GiftCodeRedemptionResult.Redeemed, result);
        var savedCode = await _context.GiftCodes.AsNoTracking().SingleAsync(code => code.Id == giftCode.Id, ct);
        var savedUser = await _context.Users.AsNoTracking().SingleAsync(user => user.Id == 10, ct);
        Assert.Equal(GiftCodeState.Redeemed, savedCode.State);
        Assert.Equal(UtcNow, savedCode.RedeemedAtUtc);
        Assert.Equal(10, savedCode.RedeemedByUserId);
        Assert.Equal(PricingLevel.Basic, savedUser.PricingLevel);
    }

    [Fact]
    public async Task Redeem_SameCodeToTwoUsers_AllowsOnlyOneClaim()
    {
        var ct = TestContext.Current.CancellationToken;
        await AddUser(10, PricingLevel.Free, ct);
        await AddUser(11, PricingLevel.Free, ct);
        var giftCode = await AddGiftCode("C002", PricingLevel.Basic, ct);
        var repository = new GiftCodeRepository(_context);

        Assert.Equal(GiftCodeRedemptionResult.Redeemed, await repository.Redeem(giftCode.CodeHash, 10, UtcNow, ct));
        Assert.Equal(GiftCodeRedemptionResult.Unavailable, await repository.Redeem(giftCode.CodeHash, 11, UtcNow, ct));

        Assert.Equal(PricingLevel.Basic, (await _context.Users.AsNoTracking().SingleAsync(user => user.Id == 10, ct)).PricingLevel);
        Assert.Equal(PricingLevel.Free, (await _context.Users.AsNoTracking().SingleAsync(user => user.Id == 11, ct)).PricingLevel);
    }

    [Fact]
    public async Task Redeem_NonUpgradeLeavesCodeActiveForLaterUse()
    {
        var ct = TestContext.Current.CancellationToken;
        await AddUser(10, PricingLevel.Premium, ct);
        var giftCode = await AddGiftCode("C003", PricingLevel.Basic, ct);
        var repository = new GiftCodeRepository(_context);

        Assert.Equal(GiftCodeRedemptionResult.NotAnUpgrade, await repository.Redeem(giftCode.CodeHash, 10, UtcNow, ct));
        Assert.Equal(GiftCodeState.Active, (await _context.GiftCodes.AsNoTracking().SingleAsync(ct)).State);
        Assert.Equal(PricingLevel.Premium, (await _context.Users.AsNoTracking().SingleAsync(ct)).PricingLevel);
    }

    [Fact]
    public async Task Redeem_DifferentTierCodesForSameUser_KeepsTheHigherTier()
    {
        var ct = TestContext.Current.CancellationToken;
        await AddUser(10, PricingLevel.Free, ct);
        var basic = await AddGiftCode("C004", PricingLevel.Basic, ct);
        var premium = await AddGiftCode("C005", PricingLevel.Premium, ct);
        var repository = new GiftCodeRepository(_context);

        Assert.Equal(GiftCodeRedemptionResult.Redeemed, await repository.Redeem(basic.CodeHash, 10, UtcNow, ct));
        Assert.Equal(GiftCodeRedemptionResult.Redeemed, await repository.Redeem(premium.CodeHash, 10, UtcNow, ct));

        Assert.Equal(PricingLevel.Premium, (await _context.Users.AsNoTracking().SingleAsync(ct)).PricingLevel);
    }

    [Fact]
    public async Task ConcurrentTierRedemptionsForSameUser_CannotDowngradePremium()
    {
        var ct = TestContext.Current.CancellationToken;
        var path = Path.Combine(Path.GetTempPath(), $"gift-codes-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path};Default Timeout=10;Pooling=False")
            .Options;

        try
        {
            await using (var seed = new AppDbContext(options))
            {
                await seed.Database.EnsureCreatedAsync(ct);
                seed.Users.Add(new UserDto
                {
                    Id = 12,
                    Login = "concurrent@example.com",
                    Password = "test",
                    PricingLevel = PricingLevel.Free,
                    CreationDate = UtcNow
                });
                seed.GiftCodes.AddRange(
                    NewGiftCode("C007", PricingLevel.Basic),
                    NewGiftCode("C008", PricingLevel.Premium));
                await seed.SaveChangesAsync(ct);
            }

            using var barrier = new Barrier(2);
            var results = await Task.WhenAll(
                Task.Run(async () =>
                {
                    barrier.SignalAndWait(ct);
                    return await RedeemInOwnContext(options, new string('0', 60) + "C007", 12, ct);
                }, ct),
                Task.Run(async () =>
                {
                    barrier.SignalAndWait(ct);
                    return await RedeemInOwnContext(options, new string('0', 60) + "C008", 12, ct);
                }, ct));

            await using var verify = new AppDbContext(options);
            Assert.Equal(PricingLevel.Premium, (await verify.Users.AsNoTracking().SingleAsync(ct)).PricingLevel);
            Assert.Contains(GiftCodeRedemptionResult.Redeemed, results);
            Assert.All(results, result => Assert.True(result is
                GiftCodeRedemptionResult.Redeemed or GiftCodeRedemptionResult.NotAnUpgrade));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ConcurrentRedemptionsOfOneCode_UpgradeOnlyOneUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var path = Path.Combine(Path.GetTempPath(), $"gift-codes-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path};Default Timeout=10;Pooling=False")
            .Options;

        try
        {
            await using (var seed = new AppDbContext(options))
            {
                await seed.Database.EnsureCreatedAsync(ct);
                seed.Users.AddRange(
                    NewUser(13, "first@example.com"),
                    NewUser(14, "second@example.com"));
                seed.GiftCodes.Add(NewGiftCode("C009", PricingLevel.Basic));
                await seed.SaveChangesAsync(ct);
            }

            var hash = new string('0', 60) + "C009";
            using var barrier = new Barrier(2);
            var results = await Task.WhenAll(
                Task.Run(async () =>
                {
                    barrier.SignalAndWait(ct);
                    return await RedeemInOwnContext(options, hash, 13, ct);
                }, ct),
                Task.Run(async () =>
                {
                    barrier.SignalAndWait(ct);
                    return await RedeemInOwnContext(options, hash, 14, ct);
                }, ct));

            await using var verify = new AppDbContext(options);
            var tiers = await verify.Users.AsNoTracking().OrderBy(user => user.Id)
                .Select(user => user.PricingLevel).ToArrayAsync(ct);
            Assert.Equal(1, results.Count(result => result == GiftCodeRedemptionResult.Redeemed));
            Assert.Equal(1, results.Count(result => result == GiftCodeRedemptionResult.Unavailable));
            Assert.Equal(1, tiers.Count(level => level == PricingLevel.Basic));
            Assert.Equal(1, tiers.Count(level => level == PricingLevel.Free));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Revoke_UpdatesActiveCodeAndRejectsSecondRevocation()
    {
        var ct = TestContext.Current.CancellationToken;
        var giftCode = await AddGiftCode("C006", PricingLevel.Basic, ct);
        var repository = new GiftCodeRepository(_context);

        Assert.True(await repository.Revoke(giftCode.Id, 3, UtcNow, ct));
        Assert.False(await repository.Revoke(giftCode.Id, 4, UtcNow.AddMinutes(1), ct));

        var revoked = await _context.GiftCodes.AsNoTracking().SingleAsync(ct);
        Assert.Equal(GiftCodeState.Revoked, revoked.State);
        Assert.Equal(UtcNow, revoked.RevokedAtUtc);
        Assert.Equal(3, revoked.RevokedByUserId);
    }

    private static DateTime UtcNow => new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

    private Task AddUser(int id, PricingLevel level, CancellationToken ct)
    {
        _context.Users.Add(new UserDto
        {
            Id = id,
            Login = $"user{id}@example.com",
            Password = "test",
            PricingLevel = level,
            CreationDate = UtcNow
        });
        return _context.SaveChangesAsync(ct);
    }

    private async Task<GiftCode> AddGiftCode(string hashTail, PricingLevel level, CancellationToken ct)
    {
        var code = NewGiftCode(hashTail, level);
        _context.GiftCodes.Add(code);
        await _context.SaveChangesAsync(ct);
        return code;
    }

    private static GiftCode NewGiftCode(string hashTail, PricingLevel level) => new()
    {
        CodeHash = new string('0', 60) + hashTail,
        CodeSuffix = hashTail,
        PricingLevel = level,
        State = GiftCodeState.Active,
        CreatedAtUtc = UtcNow,
        CreatedByUserId = 1
    };

    private static UserDto NewUser(int id, string login) => new()
    {
        Id = id,
        Login = login,
        Password = "test",
        PricingLevel = PricingLevel.Free,
        CreationDate = UtcNow
    };

    private static async Task<GiftCodeRedemptionResult> RedeemInOwnContext(
        DbContextOptions<AppDbContext> options,
        string hash,
        int userId,
        CancellationToken ct)
    {
        await using var context = new AppDbContext(options);
        return await new GiftCodeRepository(context).Redeem(hash, userId, UtcNow, ct);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
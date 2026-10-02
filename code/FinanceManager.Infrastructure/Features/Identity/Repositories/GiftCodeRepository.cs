using FinanceManager.Domain.Identity.GiftCodes;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.Identity.Repositories;

public class GiftCodeRepository(AppDbContext context) : IGiftCodeRepository
{
    // The demo/test provider has no transactions or conditional UPDATE. Serialize its operations only.
    private static readonly SemaphoreSlim _inMemoryGate = new(1, 1);

    public async Task<GiftCodeDto> Add(GiftCode code, CancellationToken ct = default)
    {
        context.GiftCodes.Add(code);
        await context.SaveChangesAsync(ct);
        return ToDto(code);
    }

    public async Task<IReadOnlyList<GiftCodeDto>> List(int offset, int count, CancellationToken ct = default) =>
        (await context.GiftCodes.AsNoTracking().OrderByDescending(x => x.Id)
            .Skip(offset).Take(count).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<bool> Revoke(long id, int adminId, DateTime utcNow, CancellationToken ct = default)
    {
        if (context.Database.IsRelational())
            return await context.GiftCodes.Where(x => x.Id == id && x.State == GiftCodeState.Active)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, GiftCodeState.Revoked)
                    .SetProperty(x => x.RevokedAtUtc, utcNow).SetProperty(x => x.RevokedByUserId, adminId), ct) == 1;

        await _inMemoryGate.WaitAsync(ct);
        try
        {
            var code = await context.GiftCodes.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (code is null) return false;
            await context.Entry(code).ReloadAsync(ct);
            if (code.State != GiftCodeState.Active) return false;
            code.State = GiftCodeState.Revoked;
            code.RevokedAtUtc = utcNow;
            code.RevokedByUserId = adminId;
            await context.SaveChangesAsync(ct);
            return true;
        }
        finally { _inMemoryGate.Release(); }
    }

    public async Task<GiftCodeRedemptionResult> Redeem(string hash, int userId, DateTime utcNow, CancellationToken ct = default)
    {
        if (!context.Database.IsRelational())
            return await RedeemInMemory(hash, userId, utcNow, ct);

        // The code claim and tier upgrade commit together. Conditional updates protect both single use
        // and monotonic upgrades when different codes are redeemed concurrently for the same user.
        return await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);
            var code = await context.GiftCodes.AsNoTracking().SingleOrDefaultAsync(x => x.CodeHash == hash, ct);
            if (code is null || code.State != GiftCodeState.Active) return GiftCodeRedemptionResult.Unavailable;

            var claimed = await context.GiftCodes.Where(x => x.Id == code.Id && x.State == GiftCodeState.Active)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, GiftCodeState.Redeemed)
                    .SetProperty(x => x.RedeemedAtUtc, utcNow).SetProperty(x => x.RedeemedByUserId, userId), ct);
            if (claimed != 1) return GiftCodeRedemptionResult.Unavailable;

            var upgraded = await context.Users.Where(x => x.Id == userId && x.PricingLevel < code.PricingLevel)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.PricingLevel, code.PricingLevel), ct);
            if (upgraded != 1)
                return await context.Users.AsNoTracking().AnyAsync(x => x.Id == userId, ct)
                    ? GiftCodeRedemptionResult.NotAnUpgrade : GiftCodeRedemptionResult.UserNotFound;

            await transaction.CommitAsync(ct);
            return GiftCodeRedemptionResult.Redeemed;
        });
    }

    private async Task<GiftCodeRedemptionResult> RedeemInMemory(string hash, int userId, DateTime utcNow, CancellationToken ct)
    {
        await _inMemoryGate.WaitAsync(ct);
        try
        {
            var code = await context.GiftCodes.SingleOrDefaultAsync(x => x.CodeHash == hash, ct);
            if (code is null) return GiftCodeRedemptionResult.Unavailable;
            await context.Entry(code).ReloadAsync(ct);
            if (code.State != GiftCodeState.Active) return GiftCodeRedemptionResult.Unavailable;
            var user = await context.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
            if (user is null) return GiftCodeRedemptionResult.UserNotFound;
            await context.Entry(user).ReloadAsync(ct);
            if (user.PricingLevel >= code.PricingLevel) return GiftCodeRedemptionResult.NotAnUpgrade;
            user.PricingLevel = code.PricingLevel;
            code.State = GiftCodeState.Redeemed;
            code.RedeemedAtUtc = utcNow;
            code.RedeemedByUserId = userId;
            await context.SaveChangesAsync(ct);
            return GiftCodeRedemptionResult.Redeemed;
        }
        finally { _inMemoryGate.Release(); }
    }

    private static GiftCodeDto ToDto(GiftCode code) => new(code.Id, code.CodeSuffix, code.PricingLevel,
        code.Note, code.State, code.CreatedAtUtc, code.CreatedByUserId, code.RedeemedAtUtc,
        code.RedeemedByUserId, code.RevokedAtUtc, code.RevokedByUserId);
}
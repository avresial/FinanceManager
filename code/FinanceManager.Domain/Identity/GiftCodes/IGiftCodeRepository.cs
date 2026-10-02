namespace FinanceManager.Domain.Identity.GiftCodes;

public interface IGiftCodeRepository
{
    Task<GiftCodeDto> Add(GiftCode code, CancellationToken ct = default);
    Task<IReadOnlyList<GiftCodeDto>> List(int offset, int count, CancellationToken ct = default);
    Task<bool> Revoke(long id, int adminId, DateTime utcNow, CancellationToken ct = default);
    Task<GiftCodeRedemptionResult> Redeem(string hash, int userId, DateTime utcNow, CancellationToken ct = default);
}
using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Domain.Identity.GiftCodes;

public record GiftCodeDto(long Id, string CodeSuffix, PricingLevel PricingLevel, string? Note,
    GiftCodeState State, DateTime CreatedAtUtc, int CreatedByUserId,
    DateTime? RedeemedAtUtc, int? RedeemedByUserId, DateTime? RevokedAtUtc, int? RevokedByUserId);
using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Domain.Identity.GiftCodes;

public class GiftCode
{
    public long Id { get; set; }
    public required string CodeHash { get; set; }
    public required string CodeSuffix { get; set; }
    public PricingLevel PricingLevel { get; set; }
    public string? Note { get; set; }
    public GiftCodeState State { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime? RedeemedAtUtc { get; set; }
    public int? RedeemedByUserId { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public int? RevokedByUserId { get; set; }
}
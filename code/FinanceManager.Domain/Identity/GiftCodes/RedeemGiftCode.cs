using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Identity.GiftCodes;

public record RedeemGiftCode([Required, StringLength(64)] string Code);
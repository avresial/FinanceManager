using FinanceManager.Domain.Identity.Entities;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Identity.GiftCodes;

public record GenerateGiftCode(PricingLevel PricingLevel, [StringLength(200)] string? Note = null);
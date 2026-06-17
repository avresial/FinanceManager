using FinanceManager.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Identity.Commands;

public record UpdatePricingPlan(
    [Range(1, int.MaxValue)] int UserId,
    PricingLevel PricingLevel);
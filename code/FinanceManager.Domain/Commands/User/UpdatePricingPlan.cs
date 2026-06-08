using FinanceManager.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Commands.User;

public record UpdatePricingPlan(
    [Range(1, int.MaxValue)] int UserId,
    PricingLevel PricingLevel);
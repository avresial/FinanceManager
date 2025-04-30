using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.Commands.User;

public record UpdatePricingPlan(int userId, PricingLevel pricingLevel);

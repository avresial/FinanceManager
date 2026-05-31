using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.Commands.User;

public record AddUser(string UserName, string Password, PricingLevel PricingLevel, string? FirstName = null, string? LastName = null);
namespace FinanceManager.Api.Controllers.Admin;

public sealed record AiFallbackEntryDto(string ProviderName, string Model, int Order);
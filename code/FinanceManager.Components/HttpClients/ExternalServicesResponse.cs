namespace FinanceManager.Components.HttpClients;

public sealed record ExternalServicesResponse(IReadOnlyList<ExternalServiceDto> Services);
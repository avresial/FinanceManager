namespace FinanceManager.Api.Controllers.Admin;

public sealed record ExternalServicesResponse(IReadOnlyList<ExternalServiceDto> Services);
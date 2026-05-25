namespace FinanceManager.Api.Controllers.Admin;

public sealed record UpdateModelRequest(string ModelName, bool IsEnabled);
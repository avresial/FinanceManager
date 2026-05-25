namespace FinanceManager.Api.Controllers.Admin;

public sealed record UpdateFallbackRequest(
    IReadOnlyList<AiFallbackEntryDto> Entries);
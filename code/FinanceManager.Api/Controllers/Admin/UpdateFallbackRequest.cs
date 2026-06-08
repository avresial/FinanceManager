using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Api.Controllers.Admin;

public sealed record UpdateFallbackRequest(
    [Required, MinLength(1)] IReadOnlyList<AiFallbackEntryDto> Entries);
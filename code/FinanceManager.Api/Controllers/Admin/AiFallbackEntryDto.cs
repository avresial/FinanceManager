using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Api.Controllers.Admin;

public sealed record AiFallbackEntryDto(
    [Required, StringLength(256)] string ProviderName,
    [Required, StringLength(256)] string Model,
    [Range(0, int.MaxValue)] int Order);
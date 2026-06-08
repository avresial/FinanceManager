using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Api.Controllers.Admin;

public sealed record UpdateProviderRequest(
    [Required, Url, StringLength(2048)] string BaseUrl,
    [StringLength(4096)] string? ApiKey,
    [Range(1, 600)] int RequestTimeoutSeconds,
    bool IsEnabled);
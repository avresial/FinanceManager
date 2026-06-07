using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Api.Controllers.Admin;

public sealed record UpdateServiceKeyRequest(
    [Required, Url, StringLength(2048)] string BaseUrl,
    [StringLength(4096)] string? ApiKey,
    bool IsEnabled);

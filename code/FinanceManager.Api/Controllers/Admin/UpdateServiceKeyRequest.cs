using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Api.Controllers.Admin;

public sealed record UpdateServiceKeyRequest(
    [Required, Url] string BaseUrl,
    string? ApiKey,
    bool IsEnabled);
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Api.Controllers.Admin;

public sealed record AddProviderRequest(
    [Required, StringLength(256)] string ProviderName);
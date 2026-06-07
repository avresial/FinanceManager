using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Api.Controllers.Admin;

public sealed record UpdateModelRequest(
    [Required, StringLength(256)] string ModelName,
    bool IsEnabled);

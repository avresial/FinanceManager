using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Api.Controllers.Admin;

public sealed record AddModelRequest(
    [Required, StringLength(256)] string ModelName);

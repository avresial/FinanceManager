using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Identity.Commands;

// No range on UserId: guest sandbox accounts use negative ids, and the endpoint already restricts
// callers to their own account (or an admin).
public record UpdatePreferredCurrency(
    int UserId,
    [Range(0, int.MaxValue)] int CurrencyId);
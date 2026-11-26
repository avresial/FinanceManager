using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Transactions.Bonds.Commands;

public record UpdateBondDetails(
    [Range(1, int.MaxValue)] int Id,
    [Required, StringLength(256)] string NameToUpdate,
    [Required, StringLength(256)] string IssuerToUpdate,
    [Range(0, double.MaxValue)] decimal UnitValueToUpdate);
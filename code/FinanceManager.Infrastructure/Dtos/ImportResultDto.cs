namespace FinanceManager.Infrastructure.Dtos;

public record ImportResultDto(int AccountId, int Imported, int Failed, IReadOnlyList<string> Errors);

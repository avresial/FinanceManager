namespace FinanceManager.Domain.Shared.Ai.Entities;

public class AiFallbackEntry
{
    public int Id { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Order { get; set; }
}
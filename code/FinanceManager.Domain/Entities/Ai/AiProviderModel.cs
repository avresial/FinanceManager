namespace FinanceManager.Domain.Entities.Ai;

public class AiProviderModel
{
    public int Id { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}
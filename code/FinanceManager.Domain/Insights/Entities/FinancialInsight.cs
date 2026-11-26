using System.Text.Json.Serialization;

namespace FinanceManager.Domain.Insights.Entities;

public class FinancialInsight
{
    public int Id { get; set; }
    public int UserId { get; set; }
    // Legacy column of insights generated per account; kept for the stored history until the contract step drops it.
    [JsonIgnore]
    public int? AccountId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
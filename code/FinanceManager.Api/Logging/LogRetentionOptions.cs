namespace FinanceManager.Api.Logging;

public class LogRetentionOptions
{
    public const string SectionName = "LogRetention";

    public int RetentionDays { get; set; } = 30;
}
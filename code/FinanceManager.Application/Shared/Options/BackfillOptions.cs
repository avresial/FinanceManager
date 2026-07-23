namespace FinanceManager.Application.Shared.Options;

/// <summary>
/// Controls the startup backfill pipeline. Disable <see cref="RunOnStartup"/> for tests, migrations,
/// or environments where an external scheduler owns the process; toggle individual datasets with the
/// per-service flags.
/// </summary>
public sealed class BackfillOptions
{
    public const string SectionName = "Backfill";

    /// <summary>Master switch: when false the startup orchestrator does nothing.</summary>
    public bool RunOnStartup { get; set; }

    /// <summary>Whether the stock-price backfill service runs.</summary>
    public bool StockPricesEnabled { get; set; } = true;

    /// <summary>Whether the currency exchange-rate backfill service runs.</summary>
    public bool CurrencyRatesEnabled { get; set; } = true;

    /// <summary>
    /// How many days of history the stock backfill covers on each run. The rolling window is
    /// [today − LookbackDays, today]; the provider chain skips listings already covered in it.
    /// </summary>
    public int StockLookbackDays { get; set; } = 30;

    /// <summary>
    /// How many days of history the currency backfill requests per pair when a refresh is needed.
    /// Only the dates missing from the database are actually inserted.
    /// </summary>
    public int CurrencyLookbackDays { get; set; } = 30;
}
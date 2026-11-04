namespace FinanceManager.Domain.Shared;

public static class Percentage
{
    /// <summary>
    /// <paramref name="part"/> as a percentage of <paramref name="whole"/> (e.g. 5 of 50 is 10),
    /// or <c>null</c> when <paramref name="whole"/> is zero and the ratio is undefined.
    /// </summary>
    public static decimal? Of(decimal part, decimal whole) => whole == 0m ? null : part / whole * 100m;
}
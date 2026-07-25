using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceManager.Benchmarks.Hosting;

/// <summary>
/// Stores every <see cref="DateTimeOffset"/> property as an order-preserving integer so SQLite can
/// compare and sort them.
/// </summary>
/// <remarks>
/// <para>
/// SQLite has no native date type, and EF Core's SQLite provider refuses to translate relational
/// comparisons on <see cref="DateTimeOffset"/> at all — a <c>Where(q =&gt; q.PriceTime &lt;= asOf)</c>
/// throws "could not be translated" rather than falling back. Price quotes, and therefore every asset
/// valuation, net-worth and dashboard endpoint, are filtered exactly that way.
/// </para>
/// <para>
/// <see cref="DateTimeOffsetToBinaryConverter"/> packs the UTC ticks into the high bits and the offset
/// into the low bits, so integer ordering matches chronological ordering and the comparisons SQLite now
/// translates return the same rows a real deployment would. This lives in the benchmark project, applied
/// through EF's model-customizer hook, because production never runs on SQLite and the domain model
/// should not carry a workaround for a provider it does not use.
/// </para>
/// </remarks>
internal sealed class SqliteModelCustomizer(ModelCustomizerDependencies dependencies)
    : RelationalModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                    property.SetValueConverter(typeof(DateTimeOffsetToBinaryConverter));
            }
        }
    }
}
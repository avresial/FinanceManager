using FinanceManager.Benchmarks.Hosting;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace FinanceManager.Benchmarks.Validation;

/// <summary>Verifies the isolated synthetic long-held fixture through repositories and the full API.</summary>
internal static class LongBondFixtureValidator
{
    public static async Task<bool> Run(string[] args)
    {
        try
        {
            string Argument(string name)
            {
                var index = Array.FindIndex(args, x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (index < 0 || index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException($"Missing {name} value.");
                return args[index + 1];
            }

            var mode = Argument("--bond-mode");
            var output = Argument("--bond-output");
            if (mode is not ("fixture" or "control")) throw new ArgumentException("Mode must be fixture or control.");
            if (!Path.IsPathFullyQualified(output) || File.Exists(output)) throw new ArgumentException("Output must be an absolute path that does not exist.");

            var environment = await BenchmarkEnvironment.GetOrCreate();
            var scenario = environment.Scenario;
            using var scope = environment.CreateHostScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var fixture = await (from entry in context.BondEntries.AsNoTracking()
                                 join bond in context.Bonds on entry.BondDetailsId equals bond.Id
                                 where entry.UserId == scenario.UserId && bond.Name == "E01 synthetic long-held bond"
                                 select entry).SingleOrDefaultAsync();

            object? proof = null;
            if (mode == "control")
            {
                if (fixture is not null) throw new InvalidOperationException("Control contains the synthetic bond.");
            }
            else
            {
                if (fixture is null) throw new InvalidOperationException("Synthetic fixture entry is absent.");
                var bondId = fixture.BondDetailsId;
                var details = (await scope.ServiceProvider.GetRequiredService<IBondDetailsRepository>().GetByIdsAsync([bondId])).Single();
                var entryId = fixture.EntryId;
                var count = 0;
                decimal startPrice = 0, endPrice = 0;
                var prices = new Dictionary<DateTime, decimal>();
                var postingDate = fixture.PostingDate;
                var history = BondEntry.GetPrice(DateOnly.FromDateTime(postingDate), fixture.ValueChange, DateOnly.FromDateTime(scenario.End), details);
                for (var date = scenario.Start.Date; date <= scenario.End.Date; date = date.AddDays(1))
                {
                    var price = history[DateOnly.FromDateTime(date)];
                    if (price <= 0) throw new InvalidOperationException($"Nonpositive price on {date:yyyy-MM-dd}.");
                    prices[date] = Math.Round(price, 2);
                    if (count++ == 0) startPrice = price;
                    endPrice = price;
                }
                var ageDays = (scenario.Start.Date - postingDate.Date).Days;
                if (count == 0 || ageDays < 3650) throw new InvalidOperationException("Fixture posting age or window is invalid.");
                proof = new { bondId, entryId, postingDate, ageDays, selectedCount = count, startPrice, endPrice, prices };
                Console.WriteLine($"Selected {count} dates; start price {startPrice}, end price {endPrice}.");
            }

            async Task<JsonElement> Overview(DateTime start, DateTime end)
            {
                var route = $"api/Dashboard/overview?userId={scenario.UserId}&currencyId={scenario.CurrencyId}&start={Uri.EscapeDataString(start.ToString("O"))}&end={Uri.EscapeDataString(end.ToString("O"))}";
                using var response = await environment.Client.GetAsync(route);
                response.EnsureSuccessStatusCode();
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var series = json.RootElement.GetProperty("netWorthSeries");
                if (series.ValueKind != JsonValueKind.Array || series.GetArrayLength() == 0) throw new InvalidOperationException("netWorthSeries is empty.");
                return json.RootElement.Clone();
            }

            var full = await Overview(scenario.Start, scenario.End);
            var month = await Overview(scenario.LastMonthStart, scenario.End);
            var report = new { mode, scenario.UserId, scenario.CurrencyId, scenario.Start, scenario.End, scenario.LastMonthStart, proof, full, month };
            await using var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write);
            await JsonSerializer.SerializeAsync(stream, report, new JsonSerializerOptions { WriteIndented = true });
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return false;
        }
    }
}
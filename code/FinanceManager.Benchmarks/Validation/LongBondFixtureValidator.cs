using FinanceManager.Benchmarks.Hosting;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
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
            var repository = scope.ServiceProvider.GetRequiredService<IFinancialAccountRepository>();
            BondAccount? account = null;
            await foreach (var item in repository.GetAccounts<BondAccount>(scenario.UserId, scenario.Start, scenario.End))
                if (item.Name == "E01 synthetic long-held bond") account = item;

            object? proof = null;
            if (mode == "control")
            {
                if (account is not null) throw new InvalidOperationException("Control contains the synthetic account.");
            }
            else
            {
                if (account is null) throw new InvalidOperationException("Synthetic fixture account is absent.");
                var bondId = account.GetStoredBondsIds().Single();
                var details = (await scope.ServiceProvider.GetRequiredService<IBondDetailsRepository>().GetByIdsAsync([bondId])).Single();
                int? entryId = null;
                var count = 0;
                decimal startPrice = 0, endPrice = 0;
                var prices = new Dictionary<DateTime, decimal>();
                DateTime postingDate = default;
                for (var date = scenario.Start.Date; date <= scenario.End.Date; date = date.AddDays(1))
                {
                    var selected = account.GetThisOrNextOlder(date, bondId);
                    if (selected is null || (entryId.HasValue && selected.EntryId != entryId)) throw new InvalidOperationException($"Unexpected selection on {date:yyyy-MM-dd}.");
                    entryId = selected.EntryId;
                    postingDate = selected.PostingDate;
                    var price = selected.GetPriceAt(DateOnly.FromDateTime(date), details);
                    if (price <= 0) throw new InvalidOperationException($"Nonpositive price on {date:yyyy-MM-dd}.");
                    prices[date] = Math.Round(price, 2);
                    if (count++ == 0) startPrice = price;
                    endPrice = price;
                }
                var ageDays = (scenario.Start.Date - postingDate.Date).Days;
                if (count == 0 || ageDays < 3650) throw new InvalidOperationException("Fixture posting age or window is invalid.");
                proof = new { accountId = account.AccountId, bondId, entryId, postingDate, ageDays, selectedCount = count, startPrice, endPrice, prices };
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
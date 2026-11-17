using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using System.Net;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.HttpClients;

public class AccountContractMappingTests
{
    private const string _label = """{"id":9,"name":"Salary","classifications":[{"id":10,"labelId":9,"kind":"income","value":"regular"}]}""";
    private const string _currencyEntry = """{"description":"d","contractorDetails":"c","entryId":5000000000,"accountId":3,"postingDate":"2026-05-17T13:42:00Z","value":10,"valueChange":2,"labels":[LABEL]}""";
    private const string _bondEntry = """{"bondDetailsId":4,"description":"d","entryId":5000000000,"accountId":3,"postingDate":"2026-05-17T13:42:00Z","value":10,"valueChange":2,"labels":[LABEL]}""";

    [Fact]
    public async Task CurrencyClientReadsExistingJsonAndPreservesLabelsOnEntriesAndNeighbours()
    {
        var entry = _currencyEntry.Replace("LABEL", _label);
        using var http = new HttpClient(new JsonHandler("{\"userId\":2,\"accountId\":3,\"name\":\"Cash\",\"currencyId\":7,\"accountLabel\":4,\"entries\":[" + entry + "],\"nextOlderEntry\":" + entry + ",\"nextYoungerEntry\":" + entry + "}")) { BaseAddress = new Uri("https://localhost/") };
        var account = await new CurrencyAccountHttpClient(http).GetAccountWithEntriesAsync(3, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

        Assert.NotNull(account);
        Assert.Equal(FinanceManager.Domain.FinancialAccounts.Shared.Entities.AccountLabel.Loan, account.AccountType);
        Assert.Equal(7, account.CurrencyId);
        Assert.Equal(5_000_000_000L, Assert.Single(account.Entries).EntryId);
        Assert.Equal(13, Assert.Single(account.Entries).PostingDate.Hour);
        Assert.Equal("regular", Assert.Single(Assert.Single(account.Entries.Single().Labels).Classifications).Value);
        Assert.Equal(9, Assert.Single(account.NextOlderEntry!.Labels).Id);
        Assert.Equal(9, Assert.Single(account.NextYoungerEntry!.Labels).Id);
    }

    [Fact]
    public async Task BondClientPreservesInRangeLabelsAndExistingCarriedEntryOmission()
    {
        var entry = _bondEntry.Replace("LABEL", _label);
        using var http = new HttpClient(new JsonHandler("{\"userId\":2,\"accountId\":3,\"name\":\"Bonds\",\"accountLabel\":2,\"entries\":[" + entry + "],\"nextOlderEntries\":{\"4\":" + entry + "},\"nextYoungerEntries\":{\"4\":" + entry + "}}")) { BaseAddress = new Uri("https://localhost/") };
        var account = await new BondAccountHttpClient(http).GetAccountWithEntriesAsync(3, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

        Assert.NotNull(account);
        Assert.Equal(FinanceManager.Domain.FinancialAccounts.Shared.Entities.AccountLabel.Bond, account.AccountType);
        Assert.Equal("regular", Assert.Single(Assert.Single(account.Entries.Single().Labels).Classifications).Value);
        Assert.Equal(5_000_000_000L, account.NextOlderEntries[4].EntryId);
        Assert.Equal("d", account.NextYoungerEntries[4].Description);
        // Carried bond entries historically copy only description, not labels.
        Assert.Empty(account.NextOlderEntries[4].Labels);
        Assert.Empty(account.NextYoungerEntries[4].Labels);
    }

    [Fact]
    public async Task CurrencyClientPreservesExplicitNullClassifications()
    {
        var entry = _currencyEntry.Replace("LABEL", """{"id":9,"name":"Salary","classifications":null}""");
        using var http = new HttpClient(new JsonHandler("{\"entries\":[" + entry + "]}")) { BaseAddress = new Uri("https://localhost/") };
        var account = await new CurrencyAccountHttpClient(http).GetAccountWithEntriesAsync(3, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);
        Assert.Null(Assert.Single(Assert.Single(account!.Entries).Labels).Classifications);
    }

    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") });
    }
}
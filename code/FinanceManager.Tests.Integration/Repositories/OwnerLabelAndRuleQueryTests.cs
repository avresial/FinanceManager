using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Bond.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Infrastructure.Features.Transactions.Bonds.Repositories;
using FinanceManager.Infrastructure.Features.Transactions.Cash.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

/// <summary>
/// The owner reads and label writes behind AI labeling, the recurring-payment drill-down, transaction rules and the MCP tools, on a
/// relational provider: the owner's own id decides what is visible, whether an entry is linked to an account or not.
/// </summary>
[Trait("Category", "Integration")]
public sealed class OwnerLabelAndRuleQueryTests : IDisposable
{
    private const int _owner = 1;
    private const int _other = 2;
    private const int _guest = -3;
    private static readonly DateTime _jan1 = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly FinancialLabel _food = new() { Name = "Food" };
    private readonly FinancialLabel _rent = new() { Name = "Rent" };

    public OwnerLabelAndRuleQueryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _context.FinancialLabels.AddRange(_food, _rent);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task<long> AddCash(int owner, int? account, DateTime date, decimal change, string description = "d", params FinancialLabel[] labels)
    {
        var entry = new CurrencyAccountEntry(account, 0, date, 0, change) { UserId = owner, CurrencyId = 0, Description = description, ContractorDetails = "counterparty", Labels = [.. labels] };
        _context.CurrencyEntries.Add(entry);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entry.EntryId;
    }

    [Fact]
    public async Task CashGetByIds_ReturnsTheOwnersLinkedAndUnlinkedRowsWithLabels_NewestFirst_SkippingForeignAndDuplicateIds()
    {
        _context.Currencies.Add(new Currency(0, "PLN", "zł"));
        var unlinked = await AddCash(_owner, null, _jan1, -5, "unlinked", _food, _rent);
        var linked = await AddCash(_owner, 40, _jan1.AddDays(1), -7, "linked");
        var foreign = await AddCash(_other, null, _jan1.AddDays(2), -9);
        var guest = await AddCash(_guest, null, _jan1, -1);
        var reader = new CashTransactionRepository(_context);

        var rows = await reader.GetByIds(_owner, [unlinked, linked, foreign, linked, 99999], TestContext.Current.CancellationToken);

        Assert.Equal([linked, unlinked], rows.Select(row => row.EntryId));
        Assert.Equal(["Food", "Rent"], rows[1].Labels.Select(label => label.Name).Order());
        Assert.All(rows, row => Assert.Equal("PLN", row.Currency));
        Assert.Equal([guest], (await reader.GetByIds(_guest, [guest, unlinked], TestContext.Current.CancellationToken)).Select(row => row.EntryId));
        Assert.Empty(await reader.GetByIds(_owner, [], TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.GetByIds(0, [unlinked], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BondGetById_ReadsTheOwnersLinkedOrUnlinkedEntry_WithBondNameAndLabels_AndNothingForeign()
    {
        _context.Set<BondDetails>().Add(new BondDetails("Treasury 2030", "Issuer", new DateOnly(2025, 1, 1), new DateOnly(2030, 1, 1), []) { Id = 7 });
        var unlinked = new BondAccountEntry(null, 0, _jan1, 0, 3, 7) { UserId = _owner, Description = "bond note", Labels = [_rent] };
        var foreign = new BondAccountEntry(null, 0, _jan1, 0, 3, 7) { UserId = _other, Description = "foreign" };
        var missingDefinition = new BondAccountEntry(12, 0, _jan1, 0, 3, 99) { UserId = _owner, Description = "orphan" };
        _context.BondEntries.AddRange(unlinked, foreign, missingDefinition);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var reader = new BondTransactionRepository(_context);

        var row = await reader.GetById(_owner, unlinked.EntryId, TestContext.Current.CancellationToken);

        Assert.NotNull(row);
        Assert.Equal("Treasury 2030", row.BondName);
        Assert.Equal("Rent", Assert.Single(row.Labels).Name);
        Assert.Null(await reader.GetById(_owner, foreign.EntryId, TestContext.Current.CancellationToken));
        Assert.Null(await reader.GetById(_other, unlinked.EntryId, TestContext.Current.CancellationToken));
        var orphan = await reader.GetById(_owner, missingDefinition.EntryId, TestContext.Current.CancellationToken);
        Assert.NotNull(orphan);
        Assert.Null(orphan.BondName);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.GetById(0, unlinked.EntryId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EntryGetByIds_IsOwnerScoped_ForLinkedUnlinkedAndGuestRows()
    {
        var unlinked = await AddCash(_owner, null, _jan1, -5);
        var linked = await AddCash(_owner, 40, _jan1, -5);
        var foreign = await AddCash(_other, null, _jan1, -5);
        var guest = await AddCash(_guest, null, _jan1, -5);
        var repository = new CurrencyEntryRepository(_context);

        var entries = await repository.GetByIds(_owner, [unlinked, linked, foreign, guest], TestContext.Current.CancellationToken);

        Assert.Equal(new[] { unlinked, linked }.Order(), entries.Select(entry => entry.EntryId).Order());
        Assert.Equal([guest], (await repository.GetByIds(_guest, [unlinked, guest], TestContext.Current.CancellationToken)).Select(entry => entry.EntryId));
        Assert.Empty(await repository.GetByIds(0, [unlinked], TestContext.Current.CancellationToken));
        Assert.Empty(await repository.GetByIds(_owner, [], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EntryAddLabels_LabelsOnlyTheOwnersEntries_LinkedOrNot_AndIgnoresCrossOwnerAssignments()
    {
        var unlinked = await AddCash(_owner, null, _jan1, -5);
        var linked = await AddCash(_owner, 40, _jan1, -5);
        var foreign = await AddCash(_other, null, _jan1, -5);
        var guest = await AddCash(_guest, null, _jan1, -5);
        var repository = new CurrencyEntryRepository(_context);

        var added = await repository.AddLabels(_owner,
            [(unlinked, _food.Id), (linked, _food.Id), (linked, _food.Id), (foreign, _food.Id), (guest, _food.Id), (unlinked, 99999)], TestContext.Current.CancellationToken);
        var guestAdded = await repository.AddLabels(_guest, [(guest, _rent.Id), (unlinked, _rent.Id)], TestContext.Current.CancellationToken);

        Assert.Equal(2, added);
        Assert.Equal(1, guestAdded);
        _context.ChangeTracker.Clear();
        var stored = await _context.CurrencyEntries.Include(entry => entry.Labels).ToDictionaryAsync(entry => entry.EntryId, TestContext.Current.CancellationToken);
        Assert.Equal(["Food"], stored[unlinked].Labels.Select(label => label.Name));
        Assert.Equal(["Food"], stored[linked].Labels.Select(label => label.Name));
        Assert.Empty(stored[foreign].Labels);
        Assert.Equal(["Rent"], stored[guest].Labels.Select(label => label.Name));
        Assert.Equal(0, await repository.AddLabels(0, [(unlinked, _rent.Id)], TestContext.Current.CancellationToken));
        Assert.Equal(0, await repository.AddLabels(_owner, [(unlinked, _food.Id)], TestContext.Current.CancellationToken));
    }
}
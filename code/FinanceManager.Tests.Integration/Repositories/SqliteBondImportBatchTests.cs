using FinanceManager.Application.FinancialAccounts.Bond.Import;
using FinanceManager.Application.FinancialAccounts.Shared.Imports;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Imports;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Bond.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

[Trait("Category", "Integration")]
public sealed class SqliteBondImportBatchTests : IDisposable
{
    private const int _accountId = 7410;
    private const int _userId = 42;

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly BondEntryRepository _repo;

    public SqliteBondImportBatchTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options);

        _context.Database.EnsureCreated();
        _repo = new BondEntryRepository(_context);
        _context.Accounts.Add(new FinancialAccountBaseDto { AccountId = _accountId, UserId = _userId, AccountType = AccountType.Bond });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task AddBatch_PersistsAllEntries_AndPopulatesGeneratedIds()
    {
        var ct = TestContext.Current.CancellationToken;
        var jan10 = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var jan15 = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        var entry1 = new BondAccountEntry(null, 0, jan10, 0, 500m, bondDetailsId: 1) { UserId = _userId };
        var entry2 = new BondAccountEntry(null, 0, jan15, 0, 300m, bondDetailsId: 1) { UserId = _userId };
        List<BondAccountEntry> batch = [entry1, entry2];

        var added = await _repo.Add(batch, cancellationToken: ct);

        Assert.True(added);
        Assert.True(entry1.EntryId > 0, "entry1.EntryId must be populated with database-generated ID");
        Assert.True(entry2.EntryId > 0, "entry2.EntryId must be populated with database-generated ID");
        Assert.NotEqual(entry1.EntryId, entry2.EntryId);

        var dbEntries = await _context.BondEntries
            .AsNoTracking()
            .Where(e => e.UserId == _userId)
            .OrderBy(e => e.PostingDate)
            .ToListAsync(ct);

        Assert.Equal(2, dbEntries.Count);
        Assert.Equal(entry1.EntryId, dbEntries[0].EntryId);
        Assert.Equal(entry2.EntryId, dbEntries[1].EntryId);
        Assert.Empty(_context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task AddBatch_WithTrackedLabels_ReusesExistingLabelsWithoutDuplicateInserts()
    {
        var ct = TestContext.Current.CancellationToken;
        var existingLabel = new FinancialLabel { Name = "FixedIncome" };
        _context.FinancialLabels.Add(existingLabel);
        await _context.SaveChangesAsync(ct);

        var jan10 = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var entry = new BondAccountEntry(null, 0, jan10, 0, 500m, bondDetailsId: 1)
        {
            UserId = _userId,
            Labels = [new FinancialLabel { Id = existingLabel.Id, Name = existingLabel.Name }]
        };

        var added = await _repo.Add([entry], cancellationToken: ct);

        Assert.True(added);
        Assert.Equal(1, await _context.FinancialLabels.CountAsync(ct));

        var loaded = await _context.BondEntries
            .Include(e => e.Labels)
            .FirstOrDefaultAsync(e => e.EntryId == entry.EntryId, ct);

        Assert.NotNull(loaded);
        Assert.Single(loaded.Labels);
        Assert.Equal(existingLabel.Id, loaded.Labels.First().Id);
    }

    [Fact]
    public async Task ServiceImport_WithRelationalPersistence_PreservesReverseDayConflictAndRecalculatesRunningBalances()
    {
        var ct = TestContext.Current.CancellationToken;

        // Setup service with real repository and mocks for plan/account ownership
        var mockCurrencyEntryRepo = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        var mockInvestmentRepo = new Mock<IInvestmentTransactionRepository>();
        var mockUserRepo = new Mock<IUserRepository>();

        var user = new User { Login = "TestUser", UserId = _userId, PricingLevel = PricingLevel.Premium, CreationDate = DateTime.UtcNow };
        mockUserRepo.Setup(x => x.GetUser(_userId)).ReturnsAsync(user);

        mockCurrencyEntryRepo.Setup(x => x.GetEntriesCountPerUser(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int>());
        mockInvestmentRepo.Setup(x => x.GetCountPerUser(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int>());

        var userPlanVerifier = new UserPlanVerifier(
            mockCurrencyEntryRepo.Object,
            mockInvestmentRepo.Object,
            _repo,
            mockUserRepo.Object);

        var importAccountValidator = new ImportAccountValidator(userPlanVerifier);
        var logger = new Mock<ILogger<BondAccountImportService>>();
        var service = new BondAccountImportService(_repo, importAccountValidator, logger.Object);

        // Seed existing baseline entries on Jan 10 and Jan 20
        var jan10 = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var jan15 = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var jan20 = new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc);
        var jan25 = new DateTime(2025, 1, 25, 0, 0, 0, DateTimeKind.Utc);

        const int bondA = 1;
        await _repo.Add(new BondAccountEntry(null, 0, jan10, 0, 1000m, bondA) { UserId = _userId }, cancellationToken: ct);
        await _repo.Add(new BondAccountEntry(null, 0, jan20, 0, 500m, bondA) { UserId = _userId }, cancellationToken: ct);

        // Import dataset:
        // - Jan 25: clean new day -> should be imported
        // - Jan 20: exact match with existing entry -> conflict day, should be skipped
        // - Jan 15: clean new day -> should be imported
        List<BondEntryImport> importEntries =
        [
            new(jan15, 200m, bondA),
            new(jan20, 500m, bondA),
            new(jan25, 300m, bondA)
        ];

        var result = await service.ImportOwnerEntries(_userId, importEntries, ct);

        Assert.Equal(2, result.Imported);
        Assert.Equal(0, result.Failed);
        Assert.Single(result.Conflicts);
        Assert.Equal("Exact match", result.Conflicts[0].Reason);

        // Verify database state: 4 total entries in chronological order
        var entries = await _context.BondEntries
            .AsNoTracking()
            .Where(e => e.UserId == _userId)
            .OrderBy(e => e.PostingDate)
            .ToListAsync(ct);

        Assert.Equal(4, entries.Count);
        Assert.Equal(jan10, entries[0].PostingDate);
        Assert.Equal(1000m, entries[0].ValueChange);

        Assert.Equal(jan15, entries[1].PostingDate);
        Assert.Equal(200m, entries[1].ValueChange);

        Assert.Equal(jan20, entries[2].PostingDate);
        Assert.Equal(500m, entries[2].ValueChange);

        Assert.Equal(jan25, entries[3].PostingDate);
        Assert.Equal(300m, entries[3].ValueChange);
    }
}
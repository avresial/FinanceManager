using FinanceManager.Domain.Identity.Dtos;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Exceptions;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.Identity.Repositories;

public class UserRepository(AppDbContext context) : IUserRepository
{
    public async Task<bool> AddUser(string login, string password, PricingLevel pricingLevel, UserRole userRole, string? firstName = null, string? lastName = null)
    {
        context.Add(new UserDto
        {
            Login = login,
            FirstName = firstName,
            LastName = lastName,
            Password = password,
            PricingLevel = pricingLevel,
            UserRole = userRole,
            CreationDate = DateTime.UtcNow,
        });

        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The unique login index rejected the insert. If a row with this login now exists, a concurrent request
            // created it between the caller's existence check and this insert; translate the persistence-specific
            // failure into a domain concept so callers don't have to depend on EF Core. Otherwise the failure is
            // something else and must surface unchanged.
            context.ChangeTracker.Clear();
            if (await context.Users.AsNoTracking().AnyAsync(x => x.Login == login))
                throw new DuplicateLoginException(login);
            throw;
        }

        return true;
    }

    public async Task<bool> AddUserWithId(int userId, string login, string password, PricingLevel pricingLevel, UserRole userRole, string? firstName = null, string? lastName = null)
    {
        context.Add(new UserDto
        {
            Id = userId,
            Login = login,
            FirstName = firstName,
            LastName = lastName,
            Password = password,
            PricingLevel = pricingLevel,
            UserRole = userRole,
            CreationDate = DateTime.UtcNow,
        });

        await context.SaveChangesAsync();

        return true;
    }
    public async Task<User?> GetUser(string login, string password)
    {
        var userDto = await context.Users.FirstOrDefaultAsync(x => x.Login == login);
        if (userDto is null || userDto.Password != password) return null;

        return userDto.ToUser();
    }
    public async Task<User?> GetUser(string login)
    {
        var userDto = await context.Users.FirstOrDefaultAsync(x => x.Login == login);
        if (userDto is null) return null;

        return userDto.ToUser();
    }
    public async Task<User?> GetUser(int id)
    {
        var userDto = await context.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (userDto is null) return null;

        return userDto.ToUser();
    }
    public IAsyncEnumerable<User> GetUsers(int recordIndex, int recordsCount) => context.Users
        .OrderBy(x => x.Id)
        .Skip(recordIndex)
        .Take(recordsCount)
        .Select(x => x.ToUser())
        .ToAsyncEnumerable();

    public IAsyncEnumerable<User> GetUsers(DateTime startDate, DateTime endDate) => context.Users
        .Where(x => x.CreationDate >= startDate && x.CreationDate <= endDate)
        .Select(x => x.ToUser())
        .ToAsyncEnumerable();

    public Task<int> GetUsersCount() => context.Users.CountAsync();

    public IAsyncEnumerable<int> GetUsersIds(int recordIndex, int recordsCount) => context.Users
        .OrderBy(x => x.Id)
        .Skip(recordIndex)
        .Take(recordsCount)
        .Select(x => x.Id)
        .ToAsyncEnumerable();

    public async Task<bool> RemoveUser(int userId, CancellationToken cancellationToken = default)
    {
        // Guest sandboxes live in their own in-memory database that is dropped as a whole, so they never reach this delete.
        if (userId == 0) return false;

        if (!context.Database.IsRelational())
            return await RemoveUserTracked(userId, cancellationToken);

        // One transaction, so a failure part-way leaves the user complete instead of half deleted.
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            if (!await context.Users.AnyAsync(x => x.Id == userId, cancellationToken)) return false;

            // Labels are shared rows; a bond entry only points at its label through this optional shadow key, which must be cleared before the entry goes.
            foreach (var labels in LabelsOnBondEntries(userId))
                await labels.ExecuteUpdateAsync(set => set.SetProperty(label => EF.Property<long?>(label, _bondEntryLabelKey), (long?)null), cancellationToken);
            foreach (var rows in OwnedRowSets(userId))
                await rows.Delete(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    private async Task<bool> RemoveUserTracked(int userId, CancellationToken cancellationToken)
    {
        if (!await context.Users.AnyAsync(x => x.Id == userId, cancellationToken)) return false;

        // The in-memory provider has neither ExecuteDelete nor cascades: load the same rows, detach their labels
        // (the shared label rows stay) and remove everything in one SaveChanges.
        foreach (var rows in OwnedRowSets(userId))
            await rows.RemoveTracked(cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private const string _bondEntryLabelKey = "BondAccountEntryEntryId";

    private IEnumerable<IQueryable<FinancialLabel>> LabelsOnBondEntries(int userId)
    {
        yield return context.FinancialLabels.Where(label => context.BondEntries.Any(e => e.UserId == userId && e.EntryId == EF.Property<long?>(label, _bondEntryLabelKey)));
    }

    // Every table that holds the user's rows, each defined ONCE as a query. The relational path deletes it in place, the
    // in-memory path loads it, so the two paths cannot drift apart. Entries and dividends have no foreign key to the user
    // (they need not be linked to an account), so they are matched by their own owner key first and by the user's legacy
    // accounts second: two statements per table, because one OR over two indexes cannot use either of them. The statements
    // on legacy accounts and the Accounts table itself go when accounts are dropped (#1139); Accounts must stay after the
    // dividends that reference them.
    private IEnumerable<RowSet> OwnedRowSets(int userId)
    {
        long ownerKey = userId;
        yield return RowSet.Of(context, context.BondEntries.Where(e => e.UserId == userId), q => q.Include(e => e.Labels), e => e.Labels.Clear());
        yield return RowSet.Of(context, context.CurrencyEntries.Where(e => e.UserId == userId), q => q.Include(e => e.Labels), e => e.Labels.Clear());
        yield return RowSet.Of(context, context.InvestmentTransactions.Where(e => e.UserId == ownerKey), q => q.Include(e => e.Labels), e => e.Labels.Clear());
        yield return RowSet.Of(context, context.DividendPayments.Where(e => e.UserId == userId));
        yield return RowSet.Of(context, context.FinancialAlerts.Where(e => e.UserId == userId));
        yield return RowSet.Of(context, context.FinancialInsights.Where(e => e.UserId == userId));
        yield return RowSet.Of(context, context.TransactionRules.Where(e => e.UserId == userId));
        yield return RowSet.Of(context, context.RecurringSubscriptions.Where(e => e.UserId == userId));
        yield return RowSet.Of(context, context.RefreshTokens.Where(e => e.UserId == userId));
        yield return RowSet.Of(context, context.PasswordResetTokens.Where(e => e.UserId == userId));
        yield return RowSet.Of(context, context.Users.Where(e => e.Id == userId));
    }

    /// <summary>One table's rows of the user as a query, with the two ways to remove them.</summary>
    private sealed class RowSet(Func<CancellationToken, Task> delete, Func<CancellationToken, Task> removeTracked)
    {
        public Task Delete(CancellationToken cancellationToken) => delete(cancellationToken);
        public Task RemoveTracked(CancellationToken cancellationToken) => removeTracked(cancellationToken);

        public static RowSet Of<T>(AppDbContext context, IQueryable<T> rows, Func<IQueryable<T>, IQueryable<T>>? loadRelated = null, Action<T>? detach = null)
            where T : class => new(
                cancellationToken => rows.ExecuteDeleteAsync(cancellationToken),
                async cancellationToken =>
                {
                    var tracked = await (loadRelated?.Invoke(rows) ?? rows).ToListAsync(cancellationToken);
                    if (detach is not null) tracked.ForEach(detach);
                    context.RemoveRange(tracked);
                });
    }

    public async Task<LoginThrottlingState?> GetLoginThrottlingState(string login)
    {
        var userDto = await context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Login == login);
        if (userDto is null) return null;

        return new LoginThrottlingState(userDto.FailedLoginAttempts, userDto.LockoutEndUtc);
    }

    public async Task<int?> IncrementFailedLoginAttempts(string login)
    {
        // Relational providers can increment in a single atomic UPDATE, so concurrent failed logins can't read the
        // same value and overwrite each other's increment (a read-modify-write would lose updates and delay lockout).
        if (context.Database.IsRelational())
        {
            var affected = await context.Users
                .Where(x => x.Login == login)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.FailedLoginAttempts, u => u.FailedLoginAttempts + 1));
            if (affected == 0) return null;

            // ExecuteUpdate bypasses the change tracker and returns only the row count, so re-read the new value
            // untracked. A concurrent increment can only make this larger, which trips the lockout earlier — the
            // safe direction.
            return await context.Users.AsNoTracking()
                .Where(x => x.Login == login)
                .Select(x => (int?)x.FailedLoginAttempts)
                .FirstOrDefaultAsync();
        }

        // The in-memory provider used by tests doesn't support ExecuteUpdate; fall back to a tracked read-modify-write.
        // It only ever runs single-threaded there, so the lost-update race the atomic path guards against can't occur.
        var userDto = await context.Users.FirstOrDefaultAsync(x => x.Login == login);
        if (userDto is null) return null;

        userDto.FailedLoginAttempts++;
        await context.SaveChangesAsync();
        return userDto.FailedLoginAttempts;
    }

    public async Task<bool> LockAccount(string login, DateTime lockoutEndUtc)
    {
        if (context.Database.IsRelational())
        {
            var affected = await context.Users
                .Where(x => x.Login == login)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.LockoutEndUtc, lockoutEndUtc)
                    .SetProperty(u => u.FailedLoginAttempts, 0));
            return affected > 0;
        }

        var userDto = await context.Users.FirstOrDefaultAsync(x => x.Login == login);
        if (userDto is null) return false;

        userDto.LockoutEndUtc = lockoutEndUtc;
        userDto.FailedLoginAttempts = 0;
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ResetLoginThrottling(string login)
    {
        if (context.Database.IsRelational())
        {
            var affected = await context.Users
                .Where(x => x.Login == login)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.LockoutEndUtc, (DateTime?)null)
                    .SetProperty(u => u.FailedLoginAttempts, 0));
            return affected > 0;
        }

        var userDto = await context.Users.FirstOrDefaultAsync(x => x.Login == login);
        if (userDto is null) return false;

        userDto.LockoutEndUtc = null;
        userDto.FailedLoginAttempts = 0;
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdatePassword(int userId, string password)
    {
        var user = await context.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null) return await Task.FromResult(false);

        user.Password = password;
        await context.SaveChangesAsync();

        return await Task.FromResult(true);
    }
    public async Task<bool> UpdatePreferredCurrency(int userId, int currencyId)
    {
        var user = await context.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null) return false;

        // The entity is already tracked; change tracking persists just this property.
        user.PreferredCurrencyId = currencyId;
        await context.SaveChangesAsync();

        return true;
    }
    public async Task<bool> UpdatePreferredBenchmark(int userId, long? assetListingId)
    {
        var user = await context.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null) return false;

        user.PreferredBenchmarkListingId = assetListingId;
        await context.SaveChangesAsync();

        return true;
    }
    public async Task<bool> UpdateRole(int userId, UserRole userRole)
    {
        var user = await context.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null) return false;

        user.UserRole = userRole;
        await context.SaveChangesAsync();

        return true;
    }

    public async Task<IReadOnlyList<int>> GetDistinctPreferredCurrencyIds(CancellationToken ct = default) =>
        await context.Users
            .AsNoTracking()
            .Select(x => x.PreferredCurrencyId)
            .Distinct()
            .ToListAsync(ct);
}
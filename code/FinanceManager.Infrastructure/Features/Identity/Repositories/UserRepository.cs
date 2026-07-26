using FinanceManager.Domain.Identity.Dtos;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Exceptions;
using FinanceManager.Domain.Identity.Repositories;
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

    public async Task<bool> RemoveUser(int userId)
    {
        var userToRemove = await context.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (userToRemove is null) return await Task.FromResult(false);

        context.Remove(userToRemove);

        await context.SaveChangesAsync();
        return await Task.FromResult(true);
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
        context.Update(user);
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
    public async Task<bool> UpdatePricingPlan(int userId, PricingLevel pricingLevel)
    {
        var user = await context.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null) return await Task.FromResult(false);

        user.PricingLevel = pricingLevel;
        context.Update(user);
        await context.SaveChangesAsync();

        return await Task.FromResult(true);
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
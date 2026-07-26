using FinanceManager.Application.Commands.Login;

namespace FinanceManager.Api.Features.Identity.Guest;

/// <summary>
/// Creates a fresh in-memory guest sandbox (session + seeded demo data) and mints a guest-scoped access token.
/// Shared by the public "Check out demo" login and the develop-only auto test login endpoint.
/// </summary>
public interface IGuestLoginService
{
    /// <summary>Returns the login response for a newly seeded guest sandbox, or <c>null</c> when seeding failed.</summary>
    Task<LoginResponseModel?> LoginAsGuest(CancellationToken cancellationToken = default);
}
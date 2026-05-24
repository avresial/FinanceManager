using System.Security.Claims;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Http;

namespace FinanceManager.Api.Services.Guest;

public sealed class GuestSessionAccessor(IHttpContextAccessor httpContextAccessor) : IGuestSessionAccessor
{
    private int? _explicitGuestUserId;

    public bool IsGuest => GuestUserId.HasValue;

    public int? GuestUserId
    {
        get
        {
            if (_explicitGuestUserId.HasValue) return _explicitGuestUserId;

            var principal = httpContextAccessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true) return null;

            var isGuestClaim = principal.FindFirst(GuestClaims.IsGuest)?.Value;
            if (!string.Equals(isGuestClaim, "true", StringComparison.OrdinalIgnoreCase)) return null;

            var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(idClaim, out var id) ? id : null;
        }
    }

    public void SetGuestUserId(int guestUserId) => _explicitGuestUserId = guestUserId;
}

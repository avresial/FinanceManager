using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Components.Features.Identity.Services;

/// <summary>Single source of the name the UI shows for a user, so the sidebar and Settings never disagree.</summary>
public static class UserDisplayName
{
    public static string From(User user) =>
        !string.IsNullOrWhiteSpace(user.FirstName) ? user.FirstName.Trim() : user.Login;
}
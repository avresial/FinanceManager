namespace FinanceManager.Domain.Identity.Entities;

public enum UserRole
{
    User,
    Admin
}

public static class UserRoleExtensions
{
    public static IReadOnlyList<string> GetAssignedRoles(this UserRole role) =>
        role == UserRole.Admin ? [nameof(UserRole.User), nameof(UserRole.Admin)] : [nameof(UserRole.User)];
}
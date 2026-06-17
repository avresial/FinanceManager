namespace FinanceManager.Domain.Identity.Exceptions;

/// <summary>
/// Thrown when an attempt is made to persist a user whose login already exists. Lets callers react to a lost
/// check-then-insert race (enforced by the unique login index) without depending on any persistence-specific
/// exception type.
/// </summary>
public class DuplicateLoginException(string login)
    : Exception($"A user with login '{login}' already exists.")
{
    public string Login { get; } = login;
}
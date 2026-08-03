namespace FinanceManager.Components.Shared.Services;

/// <summary>
/// Reads the presence of script-readable browser cookies. Deliberately exposes only existence and not the value:
/// the app's script-readable cookies are presence hints, and callers should never come to depend on their content.
/// Abstracted from JS interop so consumers stay unit-testable.
/// </summary>
public interface IBrowserCookieReader
{
    Task<bool> Exists(string name, CancellationToken cancellationToken = default);
}
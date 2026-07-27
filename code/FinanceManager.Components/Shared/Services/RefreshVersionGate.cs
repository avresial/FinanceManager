namespace FinanceManager.Components.Shared.Services;

/// <summary>
/// Guards a surface against an older, slower load overwriting a newer one. Every load claims
/// an incrementing version up front and only commits its result while that version is still
/// the latest claimed one.
/// </summary>
/// <remarks>
/// One gate instance per refreshable surface — typically a private readonly field on the
/// component that owns the state being reloaded.
/// </remarks>
public sealed class RefreshVersionGate
{
    private int _version;

    /// <summary>Claims the next version for a load that is about to start.</summary>
    public int Claim() => Interlocked.Increment(ref _version);

    /// <summary>
    /// <c>true</c> while <paramref name="version"/> is still the most recently claimed one,
    /// i.e. no newer load has started since.
    /// </summary>
    public bool IsCurrent(int version) => Volatile.Read(ref _version) == version;
}
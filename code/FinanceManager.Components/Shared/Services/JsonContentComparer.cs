using System.Text.Json;

namespace FinanceManager.Components.Shared.Services;

/// <summary>
/// Default content equality for rendered models: two models are equal when they serialize to
/// the same JSON. Suits the DTO/record shapes the UI renders and spares callers from writing
/// the comparison by hand.
/// </summary>
/// <remarks>
/// Content equality is evaluated on the <em>rendered model</em>, never on the stored snapshot,
/// so snapshot metadata such as <c>FetchedAtUtc</c> can never make an unchanged surface look
/// changed. Supply a custom <see cref="IEqualityComparer{T}"/> when a model carries fields that
/// move on every fetch but do not affect what the user sees.
/// </remarks>
public sealed class JsonContentComparer<T> : IEqualityComparer<T> where T : class
{
    public static readonly JsonContentComparer<T> Default = new();

    public bool Equals(T? x, T? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        return Serialize(x) == Serialize(y);
    }

    public int GetHashCode(T obj) => Serialize(obj).GetHashCode(StringComparison.Ordinal);

    private static string Serialize(T value) => JsonSerializer.Serialize(value);
}
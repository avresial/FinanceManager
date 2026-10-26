using FinanceManager.Components.Features.Alerts.Components;
using Microsoft.AspNetCore.Components;
using System.Reflection;
using System.Text.Json;

namespace FinanceManager.Tests.Architecture;

/// <summary>
/// Keeps docs/agents/features.json (the catalog testing agents and scripts/Screenshot.cs navigate by) in sync
/// with the routable pages: every @page template is either covered by a feature or explicitly excluded.
/// </summary>
[Trait("Category", "Architecture")]
public class FeatureCatalogTests
{
    private static readonly string[] _pageTemplates = typeof(AlertsPage).Assembly
        .GetTypes()
        .SelectMany(type => type.GetCustomAttributes<RouteAttribute>())
        .Select(route => route.Template)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    [Fact]
    public void EveryPageIsInTheCatalogOrExcluded()
    {
        var catalog = LoadCatalog();
        var listed = catalog.FeaturePages.Concat(catalog.ExcludedPages).ToHashSet(StringComparer.Ordinal);

        var missing = _pageTemplates.Where(template => !listed.Contains(template)).ToArray();

        Assert.True(missing.Length == 0,
            $"Add these pages to docs/agents/features.json (a feature, or excludedPages with a reason): {string.Join(", ", missing)}");
    }

    [Fact]
    public void CatalogOnlyNamesExistingPages()
    {
        var catalog = LoadCatalog();

        var stale = catalog.FeaturePages.Concat(catalog.ExcludedPages)
            .Where(page => !_pageTemplates.Contains(page, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.True(stale.Length == 0, $"docs/agents/features.json names pages that no longer exist: {string.Join(", ", stale)}");
    }

    [Fact]
    public void FeatureIdsAreUniqueAndDescribed()
    {
        var catalog = LoadCatalog();

        Assert.Equal(catalog.Ids.Count, catalog.Ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.DoesNotContain(catalog.Descriptions, string.IsNullOrWhiteSpace);
    }

    private static Catalog LoadCatalog()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindCatalogPath()));
        var features = document.RootElement.GetProperty("features").EnumerateArray().ToArray();
        return new Catalog(
            [.. features.Select(feature => feature.GetProperty("id").GetString()!)],
            [.. features.Select(feature => feature.GetProperty("page").GetString()!)],
            [.. features.Select(feature => feature.GetProperty("description").GetString() ?? "")],
            [.. document.RootElement.GetProperty("excludedPages").EnumerateObject().Select(property => property.Name)]);
    }

    private static string FindCatalogPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "agents", "features.json");
            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException("Could not find docs/agents/features.json above the test output directory.");
    }

    private sealed record Catalog(List<string> Ids, List<string> FeaturePages, List<string> Descriptions, List<string> ExcludedPages);
}

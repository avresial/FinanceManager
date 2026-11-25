namespace FinanceManager.Application.Transactions.Import;

// Shared day-level conflict-detection algorithm for account imports. The matching
// algorithm lives here once; each account type keeps its own comparison rules by
// supplying the key selectors (currency keys on date + value, stock additionally on
// ISIN, bond on bond details). The detector returns the raw imported / existing
// entries involved so the calling service can build its own conflict type and reason.
public static class ImportConflictDetector
{
    // Imports paired with existing entries that share the same key. When a key has
    // duplicates on both sides, the first N (= the smaller count) are paired together.
    public static IEnumerable<(TImport Import, TExisting Existing)> GetExactMatches<TImport, TExisting, TKey>(
        IReadOnlyCollection<TImport> imports,
        IReadOnlyCollection<TExisting> existing,
        Func<TImport, TKey> importKey,
        Func<TExisting, TKey> existingKey)
    {
        foreach (var group in imports.GroupBy(importKey))
        {
            var importItems = group.ToList();
            var matchingExisting = existing.Where(e => EqualityComparer<TKey>.Default.Equals(existingKey(e), group.Key)).ToList();
            if (matchingExisting.Count == 0) continue;

            for (int i = 0; i < Math.Min(importItems.Count, matchingExisting.Count); i++)
                yield return (importItems[i], matchingExisting[i]);
        }
    }

    // Imports whose key occurs more often in the import set than in existing entries.
    // The surplus is the trailing imports of the group, so each one keeps its own description and contractor.
    public static IEnumerable<TImport> GetImportsMissingFromExisting<TImport, TExisting, TKey>(
        IReadOnlyCollection<TImport> imports,
        IReadOnlyCollection<TExisting> existing,
        Func<TImport, TKey> importKey,
        Func<TExisting, TKey> existingKey)
    {
        foreach (var group in imports.GroupBy(importKey))
        {
            var importItems = group.ToList();
            var matchingExistingCount = existing.Count(e => EqualityComparer<TKey>.Default.Equals(existingKey(e), group.Key));

            for (int i = matchingExistingCount; i < importItems.Count; i++)
                yield return importItems[i];
        }
    }

    // Existing entries whose key occurs more often in existing entries than in the import set.
    public static IEnumerable<TExisting> GetExistingMissingFromImports<TImport, TExisting, TKey>(
        IReadOnlyCollection<TExisting> existing,
        IReadOnlyCollection<TImport> imports,
        Func<TExisting, TKey> existingKey,
        Func<TImport, TKey> importKey)
    {
        foreach (var group in existing.GroupBy(existingKey))
        {
            var existingItems = group.ToList();
            var matchingImportsCount = imports.Count(i => EqualityComparer<TKey>.Default.Equals(importKey(i), group.Key));
            if (existingItems.Count <= matchingImportsCount) continue;

            for (int i = matchingImportsCount; i < existingItems.Count; i++)
                yield return existingItems[i];
        }
    }
}
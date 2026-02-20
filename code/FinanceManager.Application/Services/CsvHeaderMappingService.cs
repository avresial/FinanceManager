using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Services;

public class CsvHeaderMappingService(ICsvHeaderMappingRepository mappingRepository, ILogger<CsvHeaderMappingService> logger) : ICsvHeaderMappingService
{
    private const double _similarityThreshold = 0.7;

    public async Task<List<HeaderMappingResultDto>> GetSuggestedMappingsAsync(IEnumerable<string> headers)
    {
        var userMappings = await mappingRepository.GetAllMappings();
        var results = new List<HeaderMappingResultDto>();

        var standardFields = new[] { "PostingDate", "ValueChange", "ContractorDetails", "Description" };

        foreach (var header in headers)
        {
            // First, try exact match (case-insensitive)
            var exactMatch = userMappings
                .Where(m => m.HeaderName.Equals(header, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.UpdatedAt)
                .FirstOrDefault();

            if (exactMatch is not null)
            {
                results.Add(new HeaderMappingResultDto(header, exactMatch.FieldName));
                continue;
            }

            // Second, try fuzzy matching with existing mappings
            var fuzzyMatch = FindBestFuzzyMatch(header, userMappings, standardFields);
            if (fuzzyMatch is not null)
            {
                results.Add(new HeaderMappingResultDto(header, fuzzyMatch));
                continue;
            }

            // If no match found, try to match against standard field names directly
            var standardFieldMatch = FindBestFuzzyMatch(header, standardFields);
            if (standardFieldMatch is not null)
            {
                results.Add(new HeaderMappingResultDto(header, standardFieldMatch));
            }
        }

        return results;
    }

    public async Task SaveMappingsAsync(SaveMappingRequestDto mappingRequest)
    {
        var mappingsToSave = new List<CsvHeaderMapping>();

        foreach (var item in mappingRequest.Mappings)
        {
            var mapping = new CsvHeaderMapping
            {
                HeaderName = item.HeaderName,
                FieldName = item.FieldName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            mappingsToSave.Add(mapping);
        }

        try
        {
            await mappingRepository.SaveOrUpdateMappings(mappingsToSave);
            logger.LogInformation("Saved {Count} header mappings", mappingsToSave.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save header mappings");
            throw;
        }
    }

    private string? FindBestFuzzyMatch(string header, List<CsvHeaderMapping> userMappings, string[] standardFields)
    {
        var bestScore = 0.0;
        string? bestMatch = null;

        foreach (var mapping in userMappings)
        {
            var score = CalculateSimilarity(header, mapping.HeaderName);
            if (score > bestScore && score >= _similarityThreshold)
            {
                bestScore = score;
                bestMatch = mapping.FieldName;
            }
        }

        return bestMatch;
    }

    private string? FindBestFuzzyMatch(string header, string[] standardFields)
    {
        var bestScore = 0.0;
        string? bestMatch = null;

        foreach (var field in standardFields)
        {
            var score = CalculateSimilarity(header, field);
            if (score > bestScore && score >= _similarityThreshold)
            {
                bestScore = score;
                bestMatch = field;
            }
        }

        return bestMatch;
    }

    /// <summary>
    /// Calculate similarity between two strings using Levenshtein distance.
    /// Returns a value between 0 and 1, where 1 is a perfect match.
    /// </summary>
    private static double CalculateSimilarity(string s1, string s2)
    {
        var maxLength = Math.Max(s1.Length, s2.Length);
        if (maxLength == 0) return 1.0;

        var distance = LevenshteinDistance(s1.ToLowerInvariant(), s2.ToLowerInvariant());
        return 1.0 - (double)distance / maxLength;
    }

    /// <summary>
    /// Calculate the Levenshtein distance between two strings.
    /// </summary>
    private static int LevenshteinDistance(string s1, string s2)
    {
        var length1 = s1.Length;
        var length2 = s2.Length;
        var d = new int[length1 + 1, length2 + 1];

        for (int i = 0; i <= length1; i++)
            d[i, 0] = i;

        for (int j = 0; j <= length2; j++)
            d[0, j] = j;

        for (int i = 1; i <= length1; i++)
        {
            for (int j = 1; j <= length2; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;

                d[i, j] = Math.Min(
                    Math.Min(
                        d[i - 1, j] + 1,      // deletion
                        d[i, j - 1] + 1),     // insertion
                    d[i - 1, j - 1] + cost   // substitution
                );
            }
        }

        return d[length1, length2];
    }
}

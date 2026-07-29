namespace FinanceManager.Domain.Identity.Commands;

public record UpdatePreferredBenchmark(int UserId, long? AssetListingId);
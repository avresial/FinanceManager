namespace FinanceManager.Domain.FinancialAccounts.Investments.Entities;

/// <summary>
/// Describes which level an <see cref="AssetIdentifier"/> applies to, because the same identifier
/// type can be assigned at different scopes (the whole asset, a share class, a single listing, etc.).
/// </summary>
public enum IdentifierScope
{
    Asset,
    ShareClass,
    Listing,
    Exchange,
    Provider
}
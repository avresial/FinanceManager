using System.Text.Json.Serialization;

namespace FinanceManager.Contracts.Dashboard;

/// <summary>One Transactions page entry; <c>kind</c> tells which account kind (and payload) it carries.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(CurrencyTransactionLogItemDto), "currency")]
[JsonDerivedType(typeof(BondTransactionLogItemDto), "bond")]
[JsonDerivedType(typeof(InvestmentTransactionLogItemDto), "investment")]
public abstract record TransactionLogItemDto(int AccountId, string AccountName);
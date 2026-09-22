namespace FinanceManager.Domain.MoneyFlow.Entities;

/// <summary>Documents an attribution component that the current data model cannot calculate.</summary>
public sealed record UnsupportedAttributionComponent(string Name, string Reason);
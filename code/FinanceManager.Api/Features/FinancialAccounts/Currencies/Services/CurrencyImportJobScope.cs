namespace FinanceManager.Api.Features.FinancialAccounts.Currencies.Services;

/// <summary>
/// What a cash import job targets, fixed when the job is created. A job with an account id is a legacy account
/// import; one without it imports into the owner's pool of <see cref="CurrencyId"/>.
/// </summary>
public sealed record CurrencyImportJobScope(int? AccountId, int? CurrencyId);
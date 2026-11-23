namespace FinanceManager.Api.Features.Labels.Services;

public sealed record LabelSetterRequest(int UserId, IReadOnlyCollection<long> EntryIds);
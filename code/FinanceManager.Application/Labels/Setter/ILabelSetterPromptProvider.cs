namespace FinanceManager.Application.Labels.Setter;

public interface ILabelSetterPromptProvider
{
    Task<string> BuildPromptAsync(string availableLabels, string entriesCsv, CancellationToken cancellationToken = default);
}
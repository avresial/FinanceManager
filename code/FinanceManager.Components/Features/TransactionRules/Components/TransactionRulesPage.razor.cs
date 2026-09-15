using FinanceManager.Application.TransactionRules.Services;
using FinanceManager.Components.Features.TransactionRules.HttpClients;
using FinanceManager.Domain.TransactionRules;
using FinanceManager.Domain.TransactionRules.Commands;
using FinanceManager.Domain.TransactionRules.Conditions;
using FinanceManager.Domain.TransactionRules.Dtos;
using FinanceManager.Domain.TransactionRules.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Features.TransactionRules.Components;

public partial class TransactionRulesPage : ComponentBase
{
    private List<TransactionRuleDto> _rules = [];
    private Guid? _editingId;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _isApplying;
    private bool _isPreviewing;
    private bool _enabled = true;
    private bool _stopProcessing;
    private bool _confirmApply;
    private string _name = string.Empty;
    private string _accountIds = string.Empty;
    private string _labels = string.Empty;
    private string? _error;
    private TransactionRuleConditionDto _condition = NewCondition();
    private TransactionRuleActionDto _action = NewAction();
    private TransactionRuleApplyResultDto? _applyResult;
    private TransactionRuleEngineResult? _preview;
    private MudForm? _ruleForm;
    private string _previewContractor = "ACME Corp";
    private string _previewDescription = "Invoice";
    private int _previewAccountId = 1;
    private decimal _previewAmount = 100m;
    private TransactionDirection _previewDirection = TransactionDirection.Expense;

    [Inject] public required TransactionRuleHttpClient HttpClient { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        _isLoading = true;
        _error = null;
        try
        {
            _rules = await HttpClient.GetAsync();
        }
        catch (Exception)
        {
            _error = "Unable to load transaction automation rules.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        _error = null;
        if (string.IsNullOrWhiteSpace(_name))
        {
            _error = "Rule name is required.";
            return;
        }

        _isSaving = true;
        try
        {
            var condition = BuildCondition();
            var action = BuildAction();
            if (_editingId is Guid id)
            {
                var saved = await HttpClient.UpdateAsync(id, new UpdateTransactionRule(_name.Trim(), [condition], [action], _enabled, _stopProcessing));
                if (saved is null) throw new InvalidOperationException();
                Snackbar.Add("Rule updated.", Severity.Success);
            }
            else
            {
                var saved = await HttpClient.CreateAsync(new CreateTransactionRule(_name.Trim(), [condition], [action], _enabled, _stopProcessing));
                if (saved is null) throw new InvalidOperationException();
                Snackbar.Add("Rule created.", Severity.Success);
            }

            await ResetForm();
            await LoadAsync();
        }
        catch (Exception)
        {
            _error = "Unable to save this rule. Check the condition and action values.";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task ToggleAsync(TransactionRuleDto rule, bool enabled)
    {
        if (!await HttpClient.SetEnabledAsync(rule.Id, enabled))
        {
            Snackbar.Add("Unable to update this rule.", Severity.Error);
            return;
        }

        await LoadAsync();
    }

    private async Task DeleteAsync(TransactionRuleDto rule)
    {
        if (!await HttpClient.DeleteAsync(rule.Id))
        {
            Snackbar.Add("Unable to delete this rule.", Severity.Error);
            return;
        }

        _rules.Remove(rule);
        if (_editingId == rule.Id) await ResetForm();
        Snackbar.Add("Rule deleted.", Severity.Success);
    }

    private async Task MoveAsync(TransactionRuleDto rule, int offset)
    {
        var current = _rules.OrderBy(x => x.Order).ToList();
        var index = current.FindIndex(x => x.Id == rule.Id);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= current.Count) return;
        (current[index], current[target]) = (current[target], current[index]);
        var reordered = await HttpClient.ReorderAsync(current.Select(x => x.Id).ToList());
        if (reordered is null)
        {
            Snackbar.Add("Unable to reorder rules.", Severity.Error);
            return;
        }

        _rules = reordered;
    }

    private void BeginEdit(TransactionRuleDto rule)
    {
        _editingId = rule.Id;
        _name = rule.Name;
        _enabled = rule.IsEnabled;
        _stopProcessing = rule.StopProcessing;
        _condition = rule.Conditions.FirstOrDefault() is { } condition ? Clone(condition) : NewCondition();
        _action = rule.Actions.FirstOrDefault() is { } action ? Clone(action) : NewAction();
        _accountIds = string.Join(",", _condition.AccountIds);
        _labels = string.Join(",", _action.Labels);
    }

    private async Task ResetForm()
    {
        _editingId = null;
        _name = string.Empty;
        _enabled = true;
        _stopProcessing = false;
        _condition = NewCondition();
        _action = NewAction();
        _accountIds = string.Empty;
        _labels = string.Empty;
        if (_ruleForm is not null)
            await _ruleForm.ResetValidationAsync();
    }

    private async Task ApplyAsync()
    {
        _isApplying = true;
        _applyResult = null;
        try
        {
            _applyResult = await HttpClient.ApplyAsync(new ApplyTransactionRules(true));
            if (_applyResult is null) throw new InvalidOperationException();
            Snackbar.Add("Rules applied to existing transactions.", Severity.Success);
        }
        catch (Exception)
        {
            _error = "Unable to apply rules to existing transactions.";
        }
        finally
        {
            _isApplying = false;
        }
    }

    private async Task PreviewAsync()
    {
        _isPreviewing = true;
        _error = null;
        try
        {
            _preview = await HttpClient.PreviewAsync(new TransactionRulePreviewFacts(
                _previewContractor, _previewDescription, _previewAccountId, _previewAmount, _previewDirection, []));
            if (_preview is null) throw new InvalidOperationException();
        }
        catch (Exception)
        {
            _error = "Unable to preview the current rules.";
        }
        finally
        {
            _isPreviewing = false;
        }
    }

    private TransactionRuleConditionDto BuildCondition()
    {
        var condition = Clone(_condition);
        if (condition.Type.Equals("Account", StringComparison.OrdinalIgnoreCase))
            condition.AccountIds = _accountIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(value => int.TryParse(value, out _)).Select(int.Parse).ToList();
        if (condition.Type.Equals("Amount", StringComparison.OrdinalIgnoreCase) && condition.MinAmount is null && condition.MaxAmount is null && condition.Threshold is null)
            condition.Threshold = 0m;
        return condition;
    }

    private TransactionRuleActionDto BuildAction()
    {
        var action = Clone(_action);
        if (action.Type.Equals("SetLabels", StringComparison.OrdinalIgnoreCase))
            action.Labels = _labels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        return action;
    }

    private static string DescribeConditions(TransactionRuleDto rule) => rule.Conditions.Count == 0
        ? "Every transaction"
        : string.Join(" and ", rule.Conditions.Select(condition => condition.Type));

    private static string DescribeActions(TransactionRuleDto rule) => rule.Actions.Count == 0
        ? "No changes"
        : string.Join(", ", rule.Actions.Select(action => action.Type));

    private static TransactionRuleConditionDto NewCondition() => new() { Type = "Contractor" };
    private static TransactionRuleActionDto NewAction() => new() { Type = "SetLabels" };

    private static TransactionRuleConditionDto Clone(TransactionRuleConditionDto source) => new()
    {
        Type = source.Type,
        Pattern = source.Pattern,
        MatchOperator = source.MatchOperator,
        IgnoreCase = source.IgnoreCase,
        AccountIds = [.. source.AccountIds],
        Direction = source.Direction,
        Threshold = source.Threshold,
        Comparison = source.Comparison,
        MinAmount = source.MinAmount,
        MaxAmount = source.MaxAmount
    };

    private static TransactionRuleActionDto Clone(TransactionRuleActionDto source) => new()
    {
        Type = source.Type,
        Value = source.Value,
        Labels = [.. source.Labels],
        ReplaceExisting = source.ReplaceExisting
    };
}
using FinanceManager.Application.TransactionRules.Services;
using FinanceManager.Components.Features.TransactionRules.HttpClients;
using FinanceManager.Domain.TransactionRules;
using FinanceManager.Domain.TransactionRules.Commands;
using FinanceManager.Domain.TransactionRules.Conditions;
using FinanceManager.Domain.TransactionRules.Dtos;
using FinanceManager.Domain.TransactionRules.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Utilities;

namespace FinanceManager.Components.Features.TransactionRules.Components;

public partial class TransactionRulesPage : ComponentBase
{
    private List<TransactionRuleDto> _rules = [];
    private Guid? _editingId;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _isTesting;
    private bool _isApplying;
    private bool _isPreviewing;
    private bool _enabled = true;
    private bool _stopProcessing;
    private bool _confirmApply;
    private string _name = string.Empty;
    private string? _error;
    private List<ConditionEditorModel> _conditions = [NewCondition()];
    private List<ActionEditorModel> _actions = [NewAction()];
    private TransactionRuleApplyResultDto? _applyResult;
    private TransactionRuleEngineResult? _preview;
    private List<TransactionRuleTestResultDto>? _testResults;
    private int _editorVersion;
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
            var conditions = _conditions.Select(condition => condition.ToDto()).ToList();
            var actions = _actions.Select(action => action.ToDto()).ToList();
            if (_editingId is Guid id)
            {
                var saved = await HttpClient.UpdateAsync(id, new UpdateTransactionRule(_name.Trim(), conditions, actions, _enabled, _stopProcessing));
                if (saved is null) throw new InvalidOperationException();
                Snackbar.Add("Rule updated.", Severity.Success);
            }
            else
            {
                var saved = await HttpClient.CreateAsync(new CreateTransactionRule(_name.Trim(), conditions, actions, _enabled, _stopProcessing));
                if (saved is null) throw new InvalidOperationException();
                Snackbar.Add("Rule created.", Severity.Success);
            }

            await ResetForm();
            await LoadAsync();
        }
        catch (FormatException ex)
        {
            _error = ex.Message;
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

    private async Task TestAsync()
    {
        _error = null;
        _testResults = null;
        if (string.IsNullOrWhiteSpace(_name))
        {
            _error = "Rule name is required.";
            return;
        }

        _isTesting = true;
        try
        {
            var command = new CreateTransactionRule(
                _name.Trim(),
                _conditions.Select(condition => condition.ToDto()).ToList(),
                _actions.Select(action => action.ToDto()).ToList(),
                _enabled,
                _stopProcessing);
            var editorVersion = _editorVersion;
            var results = await HttpClient.TestAsync(command) ?? throw new InvalidOperationException();
            if (editorVersion == _editorVersion)
                _testResults = results;
        }
        catch (FormatException ex)
        {
            _error = ex.Message;
        }
        catch (Exception)
        {
            _error = "Unable to test this rule. Check the condition and action values.";
        }
        finally
        {
            _isTesting = false;
        }
    }

    private async Task ToggleAsync(TransactionRuleDto rule, bool enabled)
    {
        try
        {
            if (!await HttpClient.SetEnabledAsync(rule.Id, enabled))
            {
                Snackbar.Add("Unable to update this rule.", Severity.Error);
                return;
            }

            await LoadAsync();
        }
        catch (Exception)
        {
            Snackbar.Add("Unable to update this rule.", Severity.Error);
        }
    }

    private async Task DeleteAsync(TransactionRuleDto rule)
    {
        try
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
        catch (Exception)
        {
            Snackbar.Add("Unable to delete this rule.", Severity.Error);
        }
    }

    private async Task MoveAsync(TransactionRuleDto rule, int offset)
    {
        var current = _rules.OrderBy(x => x.Order).ToList();
        var index = current.FindIndex(x => x.Id == rule.Id);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= current.Count) return;
        (current[index], current[target]) = (current[target], current[index]);
        try
        {
            var reordered = await HttpClient.ReorderAsync(current.Select(x => x.Id).ToList());
            if (reordered is null)
            {
                Snackbar.Add("Unable to reorder rules.", Severity.Error);
                return;
            }

            _rules = reordered;
        }
        catch (Exception)
        {
            Snackbar.Add("Unable to reorder rules.", Severity.Error);
        }
    }

    private void BeginEdit(TransactionRuleDto rule)
    {
        _testResults = null;
        _editingId = rule.Id;
        _name = rule.Name;
        _enabled = rule.IsEnabled;
        _stopProcessing = rule.StopProcessing;
        _conditions = rule.Conditions.Select(ConditionEditorModel.FromDto).ToList();
        _actions = rule.Actions.Select(ActionEditorModel.FromDto).ToList();
        EnsureEditorItems();
    }

    private async Task ResetForm()
    {
        _editingId = null;
        _name = string.Empty;
        _enabled = true;
        _stopProcessing = false;
        _conditions = [NewCondition()];
        _actions = [NewAction()];
        _testResults = null;
        if (_ruleForm is not null)
            await _ruleForm.ResetValidationAsync();
    }

    private void AddCondition()
    {
        EditorChanged();
        _conditions.Add(NewCondition());
    }

    private void RemoveCondition(ConditionEditorModel condition)
    {
        if (_conditions.Count > 1)
        {
            EditorChanged();
            _conditions.Remove(condition);
        }
    }

    private void AddAction()
    {
        EditorChanged();
        _actions.Add(NewAction());
    }

    private void RemoveAction(ActionEditorModel action)
    {
        if (_actions.Count > 1)
        {
            EditorChanged();
            _actions.Remove(action);
        }
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

    private void EnsureEditorItems()
    {
        if (_conditions.Count == 0)
            _conditions.Add(NewCondition());
        if (_actions.Count == 0)
            _actions.Add(NewAction());
    }

    private static List<int> ParseAccountIds(string value) => value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(item => int.TryParse(item, out var accountId) && accountId > 0
            ? accountId
            : throw new FormatException($"'{item}' is not a valid account ID."))
        .ToList();

    private static List<string> ParseLabels(string value) => value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToList();

    private static ConditionEditorModel NewCondition() => new();
    private static ActionEditorModel NewAction() => new();

    private sealed class ConditionEditorModel
    {
        public string Type { get; set; } = "Contractor";
        public string? Pattern { get; set; }
        public TextMatchOperator MatchOperator { get; set; } = TextMatchOperator.Contains;
        public bool IgnoreCase { get; set; } = true;
        public string AccountIdsText { get; set; } = string.Empty;
        public TransactionDirection Direction { get; set; } = TransactionDirection.Expense;
        public decimal? Threshold { get; set; }
        public AmountComparison Comparison { get; set; } = AmountComparison.GreaterThan;
        public decimal? MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }

        public static ConditionEditorModel FromDto(TransactionRuleConditionDto source) => new()
        {
            Type = source.Type,
            Pattern = source.Pattern,
            MatchOperator = source.MatchOperator,
            IgnoreCase = source.IgnoreCase,
            AccountIdsText = string.Join(",", source.AccountIds),
            Direction = source.Direction,
            Threshold = source.Threshold,
            Comparison = source.Comparison,
            MinAmount = source.MinAmount,
            MaxAmount = source.MaxAmount
        };

        public TransactionRuleConditionDto ToDto()
        {
            var condition = new TransactionRuleConditionDto
            {
                Type = Type,
                Pattern = Pattern,
                MatchOperator = MatchOperator,
                IgnoreCase = IgnoreCase,
                AccountIds = Type.Equals("Account", StringComparison.OrdinalIgnoreCase) ? ParseAccountIds(AccountIdsText) : [],
                Direction = Direction,
                Threshold = Threshold,
                Comparison = Comparison,
                MinAmount = MinAmount,
                MaxAmount = MaxAmount
            };

            if (Type.Equals("Amount", StringComparison.OrdinalIgnoreCase) && condition.MinAmount is null &&
                condition.MaxAmount is null && condition.Threshold is null)
                condition.Threshold = 0m;

            return condition;
        }
    }

    private sealed class ActionEditorModel
    {
        public string Type { get; set; } = "SetLabels";
        public string? Value { get; set; }
        public string LabelsText { get; set; } = string.Empty;
        public bool ReplaceExisting { get; set; }

        public static ActionEditorModel FromDto(TransactionRuleActionDto source) => new()
        {
            Type = source.Type,
            Value = source.Value,
            LabelsText = string.Join(",", source.Labels),
            ReplaceExisting = source.ReplaceExisting
        };

        public TransactionRuleActionDto ToDto() => new()
        {
            Type = Type,
            Value = Value,
            Labels = Type.Equals("SetLabels", StringComparison.OrdinalIgnoreCase) ? ParseLabels(LabelsText) : [],
            ReplaceExisting = ReplaceExisting
        };
    }

    private static string DescribeConditions(TransactionRuleDto rule) => rule.Conditions.Count == 0
        ? "Every transaction"
        : string.Join(" and ", rule.Conditions.Select(condition => condition.Type));

    private static string DescribeActions(TransactionRuleDto rule) => rule.Actions.Count == 0
        ? "No changes"
        : string.Join(", ", rule.Actions.Select(action => action.Type));

    private void OnEditorChanged(FormFieldChangedEventArgs _) => EditorChanged();

    private void EditorChanged()
    {
        _editorVersion++;
        _testResults = null;
    }

    private static string FormatLabels(IReadOnlyList<string> labels) => labels.Count == 0
        ? "None"
        : string.Join(", ", labels);
}
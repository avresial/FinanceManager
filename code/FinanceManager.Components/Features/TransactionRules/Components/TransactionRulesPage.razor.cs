using FinanceManager.Application.TransactionRules.Services;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.TransactionRules.HttpClients;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
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
    private List<AvailableAccount> _accounts = [];
    private bool _accountsLoaded;
    private Guid? _editingId;
    private Guid? _expandedId;
    private Guid? _confirmDeleteId;
    private bool _isCreating;
    private bool _isReordering;
    private bool _isDeleting;
    private bool _isToggling;
    private bool _testEnabled;
    private int _previewVersion;

    private bool IsBusy => _isSaving || _isTesting || _isReordering || _isDeleting || _isToggling || _isApplying || _isPreviewing;
    private TransactionRuleDto? ExpandedRule => _rules.FirstOrDefault(rule => rule.Id == _expandedId);
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
    private string _previewContractor = string.Empty;
    private string _previewDescription = string.Empty;
    private int? _previewAccountId;
    private decimal _previewAmount = 100m;
    private TransactionDirection _previewDirection = TransactionDirection.Expense;

    [Inject] public required TransactionRuleHttpClient HttpClient { get; set; }
    [Inject] public required CurrencyAccountHttpClient CurrencyAccountHttpClient { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        _isLoading = true;
        _error = null;
        try
        {
            _rules = (await HttpClient.GetAsync()).OrderBy(rule => rule.Order).ToList();
        }
        catch (Exception)
        {
            _error = "Unable to load transaction automation rules.";
        }
        try
        {
            _accounts = [.. await CurrencyAccountHttpClient.GetAvailableAccountsAsync()];
            _accountsLoaded = true;
            if (_previewAccountId is not int accountId || IsMissingAccount(accountId))
                _previewAccountId = _accounts.FirstOrDefault()?.AccountId;
        }
        catch (Exception)
        {
            _accounts = [];
            _accountsLoaded = false;
            _error ??= "Unable to load accounts for transaction automation rules.";
        }
        _isLoading = false;
    }

    private async Task RefreshAsync()
    {
        if (IsBusy) return;

        await ResetForm();
        _expandedId = null;
        _confirmDeleteId = null;
        InvalidatePreview();
        await LoadAsync();
    }

    private async Task BeginCreate()
    {
        if (IsBusy) return;

        await ResetForm();
        _expandedId = null;
        _confirmDeleteId = null;
        _isCreating = true;
    }

    private async Task ExpandRuleAsync(TransactionRuleDto rule)
    {
        var wasExpanded = _expandedId == rule.Id;
        await ResetForm();
        _confirmDeleteId = null;
        _expandedId = wasExpanded ? null : rule.Id;
    }

    private static string DetailId(TransactionRuleDto rule) => $"rule-detail-{rule.Id:N}";

    private async Task SaveAsync()
    {
        if (IsBusy) return;

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
            TransactionRuleDto? saved;
            if (_editingId is Guid id)
            {
                saved = await HttpClient.UpdateAsync(id, new UpdateTransactionRule(_name.Trim(), conditions, actions, _enabled, _stopProcessing));
                if (saved is null) throw new InvalidOperationException();
                Snackbar.Add("Rule updated.", Severity.Success);
            }
            else
            {
                saved = await HttpClient.CreateAsync(new CreateTransactionRule(_name.Trim(), conditions, actions, _enabled, _stopProcessing));
                if (saved is null) throw new InvalidOperationException();
                Snackbar.Add("Rule created.", Severity.Success);
            }

            _expandedId = saved.Id;
            await ResetForm();
            InvalidatePreview();
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

    private Task TestSavedRuleAsync(TransactionRuleDto rule) => RunTestAsync(new CreateTransactionRule(
        rule.Name, rule.Conditions, rule.Actions, rule.IsEnabled, rule.StopProcessing));

    private async Task TestAsync()
    {
        if (IsBusy) return;

        _error = null;
        if (string.IsNullOrWhiteSpace(_name))
        {
            _error = "Rule name is required.";
            return;
        }

        await RunTestAsync(new CreateTransactionRule(
            _name.Trim(),
            _conditions.Select(condition => condition.ToDto()).ToList(),
            _actions.Select(action => action.ToDto()).ToList(),
            _enabled,
            _stopProcessing));
    }

    private async Task RunTestAsync(CreateTransactionRule command)
    {
        if (IsBusy) return;

        _error = null;
        _testResults = null;
        _isTesting = true;
        var editorVersion = _editorVersion;
        try
        {
            var results = await HttpClient.TestAsync(command) ?? throw new InvalidOperationException();
            if (editorVersion == _editorVersion)
            {
                _testEnabled = command.IsEnabled;
                _testResults = results;
            }
        }
        catch (Exception)
        {
            if (editorVersion == _editorVersion)
                _error = "Unable to test this rule. Check the condition and action values.";
        }
        finally
        {
            _isTesting = false;
        }
    }

    private async Task ToggleAsync(TransactionRuleDto rule, bool enabled)
    {
        if (IsBusy) return;

        _isToggling = true;
        try
        {
            if (!await HttpClient.SetEnabledAsync(rule.Id, enabled))
            {
                Snackbar.Add("Unable to update this rule.", Severity.Error);
                return;
            }

            EditorChanged();
            InvalidatePreview();
            await LoadAsync();
        }
        catch (Exception)
        {
            Snackbar.Add("Unable to update this rule.", Severity.Error);
        }
        finally
        {
            _isToggling = false;
        }
    }

    private async Task DeleteAsync(TransactionRuleDto rule)
    {
        if (IsBusy) return;

        _isDeleting = true;
        try
        {
            if (!await HttpClient.DeleteAsync(rule.Id))
            {
                Snackbar.Add("Unable to delete this rule.", Severity.Error);
                return;
            }

            await ResetForm();
            _expandedId = null;
            _confirmDeleteId = null;
            InvalidatePreview();
            await LoadAsync();
            Snackbar.Add("Rule deleted.", Severity.Success);
        }
        catch (Exception)
        {
            Snackbar.Add("Unable to delete this rule.", Severity.Error);
        }
        finally
        {
            _isDeleting = false;
        }
    }

    private async Task MoveAsync(TransactionRuleDto rule, int offset)
    {
        if (IsBusy) return;

        var current = _rules.OrderBy(x => x.Order).ToList();
        var index = current.FindIndex(x => x.Id == rule.Id);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= current.Count) return;
        (current[index], current[target]) = (current[target], current[index]);
        _isReordering = true;
        try
        {
            var reordered = await HttpClient.ReorderAsync(current.Select(x => x.Id).ToList());
            if (reordered is null)
            {
                Snackbar.Add("Unable to reorder rules.", Severity.Error);
                return;
            }

            _rules = reordered.OrderBy(rule => rule.Order).ToList();
            InvalidatePreview();
        }
        catch (Exception)
        {
            Snackbar.Add("Unable to reorder rules.", Severity.Error);
        }
        finally
        {
            _isReordering = false;
        }
    }

    private void BeginEdit(TransactionRuleDto rule)
    {
        if (IsBusy) return;

        EditorChanged();
        _expandedId = rule.Id;
        _isCreating = false;
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
        EditorChanged();
        _isCreating = false;
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
        if (IsBusy) return;

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
        if (IsBusy) return;

        if (_previewAccountId is not int accountId)
        {
            _error = "Select an account to preview the current rules.";
            return;
        }

        _isPreviewing = true;
        _error = null;
        _preview = null;
        var previewVersion = _previewVersion;
        try
        {
            var preview = await HttpClient.PreviewAsync(new TransactionRulePreviewFacts(
                _previewContractor, _previewDescription, accountId, _previewAmount, _previewDirection, []));
            if (preview is null) throw new InvalidOperationException();
            if (previewVersion == _previewVersion)
                _preview = preview;
        }
        catch (Exception)
        {
            if (previewVersion == _previewVersion)
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

    private static List<string> ParseLabels(string value) => value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToList();

    private static ConditionEditorModel NewCondition() => new();
    private static ActionEditorModel NewAction() => new();

    private bool IsMissingAccount(int accountId) => _accountsLoaded && _accounts.All(account => account.AccountId != accountId);

    private string GetAccountLabel(AvailableAccount account)
    {
        var accountsWithSameName = _accounts
            .Where(candidate => candidate.AccountName.Equals(account.AccountName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => candidate.AccountId)
            .ToList();
        var context = account.AccountLabel?.ToString();
        if (accountsWithSameName.Count == 1)
            return context is null ? account.AccountName : $"{account.AccountName} ({context})";

        var duplicateContext = accountsWithSameName.Count(candidate => candidate.AccountLabel == account.AccountLabel) > 1;
        if (!duplicateContext && context is not null)
            return $"{account.AccountName} ({context})";

        var position = accountsWithSameName.IndexOf(account) + 1;
        return context is null
            ? $"{account.AccountName} ({position} of {accountsWithSameName.Count})"
            : $"{account.AccountName} ({context} {position} of {accountsWithSameName.Count})";
    }

    private string GetAccountLabel(int accountId)
    {
        var account = _accounts.FirstOrDefault(candidate => candidate.AccountId == accountId);
        if (account is not null) return GetAccountLabel(account);
        return _accountsLoaded ? GetMissingAccountLabel(accountId) : $"Account #{accountId}";
    }

    private static string GetMissingAccountLabel(int accountId) => $"Deleted account (#{accountId})";

    private sealed class ConditionEditorModel
    {
        public string Type { get; set; } = "Contractor";
        public string? Pattern { get; set; }
        public TextMatchOperator MatchOperator { get; set; } = TextMatchOperator.Contains;
        public bool IgnoreCase { get; set; } = true;
        public IReadOnlyCollection<int> AccountIds { get; set; } = [];
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
            AccountIds = source.AccountIds,
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
                AccountIds = Type.Equals("Account", StringComparison.OrdinalIgnoreCase) ? AccountIds.Distinct().ToList() : [],
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

    private string DescribeConditions(TransactionRuleDto rule) => rule.Conditions.Count == 0
        ? "Every transaction"
        : string.Join(" and ", rule.Conditions.Select(condition => DescribeCondition(condition, false)));

    private string DescribeCondition(TransactionRuleConditionDto condition, bool includeOptions) => condition.Type.ToLowerInvariant() switch
    {
        "contractor" or "description" => $"{condition.Type} {DescribeMatch(condition.MatchOperator)} “{condition.Pattern}”{(includeOptions ? condition.IgnoreCase ? " · ignore case" : " · case sensitive" : string.Empty)}",
        "account" => $"Account is {string.Join(" or ", condition.AccountIds.Select(GetAccountLabel))}",
        "direction" => $"Direction is {condition.Direction}",
        "amount" => DescribeAmount(condition),
        _ => condition.Type
    };

    private static string DescribeAmount(TransactionRuleConditionDto condition)
    {
        if (condition.Threshold is decimal threshold)
            return $"{condition.Direction} amount {DescribeComparison(condition.Comparison)} {MoneyFormatter.FormatNumber(threshold)}";
        var bounds = new List<string>();
        if (condition.MinAmount is decimal min) bounds.Add($"at least {MoneyFormatter.FormatNumber(min)}");
        if (condition.MaxAmount is decimal max) bounds.Add($"at most {MoneyFormatter.FormatNumber(max)}");
        return $"{condition.Direction} amount {string.Join(" and ", bounds)} (inclusive)";
    }

    private static string DescribeMatch(TextMatchOperator match) => match switch
    {
        TextMatchOperator.Equals => "equals",
        TextMatchOperator.Contains => "contains",
        TextMatchOperator.StartsWith => "starts with",
        TextMatchOperator.EndsWith => "ends with",
        TextMatchOperator.RegularExpression => "matches regular expression",
        _ => match.ToString()
    };

    private static string DescribeComparison(AmountComparison comparison) => comparison switch
    {
        AmountComparison.LessThan => "less than",
        AmountComparison.LessThanOrEqual => "at most",
        AmountComparison.Equal => "equals",
        AmountComparison.GreaterThanOrEqual => "at least",
        AmountComparison.GreaterThan => "greater than",
        _ => comparison.ToString()
    };

    private static string DescribeActions(TransactionRuleDto rule) => rule.Actions.Count == 0
        ? "No changes"
        : string.Join("; ", rule.Actions.Select(DescribeAction));

    private static string DescribeAction(TransactionRuleActionDto action) => action.Type.ToLowerInvariant() switch
    {
        "setlabels" or "labels" => $"{(action.ReplaceExisting ? "Replace existing labels with" : "Add labels")} {FormatLabels(action.Labels)}",
        "normalizecontractor" or "contractor" => $"Rename contractor to “{action.Value}”",
        "normalizedescription" or "description" => $"Set description to “{action.Value}”",
        _ => action.Type
    };

    private static string DescribeOutcome(TransactionRuleOutcome outcome) => outcome.Status switch
    {
        TransactionRuleOutcomeStatus.SkippedDisabled => "Disabled · not evaluated",
        TransactionRuleOutcomeStatus.NotMatched => "No match",
        TransactionRuleOutcomeStatus.SkippedAfterStop => "Skipped after an earlier rule stopped processing",
        TransactionRuleOutcomeStatus.StoppedProcessing => $"Matched · {(outcome.HasChanges ? "changed transaction" : "no effective change")} · stopped after actions",
        _ => $"Matched · {(outcome.HasChanges ? "changed transaction" : "no effective change")}"
    };

    private static string DisplayText(string? value) => string.IsNullOrEmpty(value) ? "None" : value;

    private void InvalidatePreview()
    {
        _previewVersion++;
        _preview = null;
    }

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
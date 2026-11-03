using FinanceManager.Application.TransactionRules.Services;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.TransactionRules.HttpClients;
using FinanceManager.Components.Features.TransactionRules.Models;
using FinanceManager.Components.Features.TransactionRules.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using FinanceManager.Domain.TransactionRules;
using FinanceManager.Domain.TransactionRules.Commands;
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
    private TransactionRuleAccountLabels _accountLabels = TransactionRuleAccountLabels.NotLoaded;
    private readonly TransactionRuleDraft _draft = new();
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
    private bool _confirmApply;
    private string? _error;
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
            _accountLabels = new TransactionRuleAccountLabels(_accounts, Loaded: true);
            if (_previewAccountId is not int accountId || _accountLabels.IsMissing(accountId))
                _previewAccountId = _accounts.FirstOrDefault()?.AccountId;
        }
        catch (Exception)
        {
            _accounts = [];
            _accountLabels = TransactionRuleAccountLabels.NotLoaded;
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
        if (!_draft.HasName)
        {
            _error = "Rule name is required.";
            return;
        }

        _isSaving = true;
        try
        {
            TransactionRuleDto? saved;
            if (_draft.EditingId is Guid id)
            {
                saved = await HttpClient.UpdateAsync(id, _draft.ToUpdateCommand());
                if (saved is null) throw new InvalidOperationException();
                Snackbar.Add("Rule updated.", Severity.Success);
            }
            else
            {
                saved = await HttpClient.CreateAsync(_draft.ToCreateCommand());
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
        if (!_draft.HasName)
        {
            _error = "Rule name is required.";
            return;
        }

        await RunTestAsync(_draft.ToCreateCommand());
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
        _draft.LoadFrom(rule);
    }

    private async Task ResetForm()
    {
        EditorChanged();
        _isCreating = false;
        _draft.Reset();
        _testResults = null;
        if (_ruleForm is not null)
            await _ruleForm.ResetValidationAsync();
    }

    private void AddCondition()
    {
        EditorChanged();
        _draft.AddCondition();
    }

    private void RemoveCondition(ConditionEditorModel condition)
    {
        if (_draft.RemoveCondition(condition))
            EditorChanged();
    }

    private void AddAction()
    {
        EditorChanged();
        _draft.AddAction();
    }

    private void RemoveAction(ActionEditorModel action)
    {
        if (_draft.RemoveAction(action))
            EditorChanged();
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
}
using FinanceManager.Components.Features.Labels.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Features.Administration.Components;

public partial class EditLabelPage : ComponentBase
{
    private bool _isUpdatingName;
    private readonly List<string> _errors = [];
    private readonly List<string> _warnings = [];
    private readonly List<string> _info = [];

    private MudForm? _nameForm;
    private FinancialLabel? _labelData;

    private bool _isLoadingPage;
    private bool _success;
    public string? NameField { get; set; }
    private string _selectedClassification = string.Empty;

    [Inject] public required FinancialLabelHttpClient FinancialLabelHttpClient { get; set; }

    [Parameter] public required int LabelId { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        _isLoadingPage = true;
        _labelData = null;
        _errors.Clear();
        _warnings.Clear();
        _info.Clear();

        try
        {
            _labelData = await FinancialLabelHttpClient.Get(LabelId);
            if (_labelData is null)
            {
                _errors.Add("Label not found.");
                _isLoadingPage = false;
                return;
            }
            NameField = _labelData.Name;
            _selectedClassification = _labelData.Classifications
                .FirstOrDefault(c => c.Kind == FinancialLabelClassificationCatalog.SpendingNecessityKind)?.Value
                ?? string.Empty;
        }
        catch (Exception)
        {
            _errors.Add("Failed to load label data. Please try again.");
        }

        _isLoadingPage = false;
    }

    private async Task SetClassificationAsync()
    {
        if (_labelData is null || string.IsNullOrEmpty(_selectedClassification)) return;

        var result = await FinancialLabelHttpClient.AddClassification(
            new(_labelData.Id, FinancialLabelClassificationCatalog.SpendingNecessityKind, _selectedClassification));

        if (!result)
        {
            _errors.Insert(0, "Failed to set classification.");
            return;
        }

        _errors.Clear();
        _info.Insert(0, $"Classification set to '{_selectedClassification}'.");

        _labelData = await FinancialLabelHttpClient.Get(_labelData.Id);
        if (_labelData is not null)
            _selectedClassification = _labelData.Classifications
                .FirstOrDefault(c => c.Kind == FinancialLabelClassificationCatalog.SpendingNecessityKind)?.Value
                ?? string.Empty;
    }
    private async Task UpdateNameAsync()
    {
        if (_isUpdatingName) return;
        _isUpdatingName = true;
        try
        {
            if (_labelData is null) return;
            if (NameField is null) return;
            if (_nameForm is null) return;

            await _nameForm.Validate();
            if (_nameForm.IsValid)
            {
                var submittedName = NameField;
                var submittedLabelId = _labelData.Id;
                var result = await FinancialLabelHttpClient.UpdateName(submittedLabelId, submittedName);
                // The page may have navigated to another label while the request ran.
                if (_labelData is null || _labelData.Id != submittedLabelId) return;
                if (!result)
                {
                    _errors.Insert(0, "Failed to change name.");
                    return;
                }
                else
                {
                    _errors.Clear();
                    _labelData.Name = submittedName;
                    _info.Insert(0, "Name changed successfully.");
                }
            }
        }
        finally
        {
            _isUpdatingName = false;
        }
    }

    private static IEnumerable<string> ValidateName(string pw)
    {
        if (!string.IsNullOrWhiteSpace(pw))
            yield break;

        yield return "Name is required!";
        yield break;
    }
}
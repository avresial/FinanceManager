using FinanceManager.Components.HttpContexts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.WebUi.Pages.Admin;
public partial class EditLabelPage
{
    private readonly List<string> _errors = [];
    private readonly List<string> _warnings = [];
    private readonly List<string> _info = [];

    private MudForm? _nameForm;
    private string _nameField { get; set; }
    private FinancialLabel? _labelData;

    private bool _isLoadingPage;
    private bool _success;

    [Inject] public required FinancialLabelHttpContext FinancialLabelHttpContext { get; set; }

    [Parameter] public required int LabelId { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        _isLoadingPage = true;

        try
        {
            _labelData = await FinancialLabelHttpContext.Get(LabelId);
            if (_labelData is null)
            {
                _errors.Add("Label not found.");
                _isLoadingPage = false;
                return;
            }
            _nameField = _labelData.Name;
        }
        catch (Exception ex)
        {
            _errors.Add($"Failed to load label data: {ex.Message}");
        }

        _isLoadingPage = false;
    }
    private async Task UpdateNameAsync()
    {
        if (_labelData is null) return;
        if (_nameField is null) return;

        await _nameForm.Validate();
        if (_nameForm.IsValid)
        {
            var result = await FinancialLabelHttpContext.UpdateName(_labelData.Id, _nameField);
            if (!result)
            {
                _errors.Insert(0, "Failed to change name.");
                return;
            }
            else
            {
                _errors.Clear();
                _info.Insert(0, "Name changed successfully.");
            }
        }
    }

    private static IEnumerable<string> ValidateName(string pw)
    {
        if (string.IsNullOrWhiteSpace(pw))
        {
            yield return "Name is required!";
            yield break;
        }
    }
}
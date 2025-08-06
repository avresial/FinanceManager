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

    [Parameter] public required int LabelId { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        _isLoadingPage = true;

        try
        {
            _labelData = new FinancialLabel() { Id = LabelId, Name = $"Test {Random.Shared.Next(0, 10)}" };
            _nameField = _labelData.Name;
        }
        catch (Exception ex)
        {
            _errors.Add($"Failed to load label data: {ex.Message}");
        }

        _isLoadingPage = false;
    }
    private async Task ChangeNameAsync()
    {
        if (_labelData is null) return;
        if (_nameField is null) return;

        await _nameForm.Validate();
        if (_nameForm.IsValid)
        {
            var result = false; // LabelService.ChangeLabelName(_labelData.Id, _nameField.Value);
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
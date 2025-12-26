using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Shared.Accounts;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.Admin;

public partial class AdminLabelPage : ComponentBase
{
    private bool _isLoading = true;
    private int _labelsCount;
    private int _elementsPerPage = 20;
    private int _pagesCount;

    private List<string> _errors = [];
    private IEnumerable<FinancialLabel> _elements = [];

    public MudTable<FinancialLabel>? Table { get; set; }
    public int SelectedPage { get; set; } = 1;

    [Inject] public required FinancialLabelHttpClient FinancialLabelHttpClient { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _labelsCount = await FinancialLabelHttpClient.GetCount();
            if (_labelsCount == 0)
            {
                NavigationManager.NavigateTo("Admin/AddLabel");
                return;
            }
        }
        catch (Exception)
        {
            _errors.Add("Failed while getting labels count");
            _isLoading = false;
            return;
        }

        try
        {
            _elements = await FinancialLabelHttpClient.Get(0, _elementsPerPage);
        }
        catch (Exception)
        {
            _errors.Add("Failed while getting labels");
            _isLoading = false;
            return;
        }

        if (_elementsPerPage != 0)
            _pagesCount = (int)Math.Ceiling((double)_labelsCount / _elementsPerPage);
        _isLoading = false;
    }

    private async Task PageChanged(int i)
    {
        try
        {
            _elements = (await FinancialLabelHttpClient.Get((i - 1) * _elementsPerPage, _elementsPerPage)).Take(_elementsPerPage).ToList();
        }
        catch (Exception)
        {
            _errors.Add("Failed while getting labels");
            return;
        }
    }

    private async Task RemoveLabel(int labelId)
    {
        var result = await FinancialLabelHttpClient.Delete(labelId);
        if (!result)
        {
            _errors.Add($"Failed to delete label {labelId}");
            return;
        }

        _elements = await FinancialLabelHttpClient.Get((SelectedPage - 1) * _elementsPerPage, _elementsPerPage);
    }
}
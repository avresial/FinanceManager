using FinanceManager.Application.Commands.Account;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Shared.Accounts;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.Admin;

public partial class AdminLabelPage : ComponentBase
{
    private const int _suggestionSampleSize = 100;
    private const int _maxSuggestions = 5;

    private bool _isLoading = true;
    private int _labelsCount;
    private int _elementsPerPage = 20;
    private int _pagesCount;
    private string _searchText = string.Empty;

    private List<string> _errors = [];
    private IReadOnlyList<FinancialLabel> _allElements = [];
    private IReadOnlyList<FinancialLabel> _filteredElements = [];
    private IEnumerable<FinancialLabel> _elements = [];

    private bool _showSuggestions;
    private bool _isLoadingSuggestions;
    private bool _suggestionsRan;
    private List<LabelSuggestion> _suggestions = [];
    private List<LabelSuggestion> _visibleSuggestions = [];
    private readonly List<string> _suggestionErrors = [];
    private readonly HashSet<string> _processing = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _dismissed = new(StringComparer.OrdinalIgnoreCase);

    public MudTable<FinancialLabel>? Table { get; set; }
    public int SelectedPage { get; set; } = 1;

    [Inject] public required FinancialLabelHttpClient FinancialLabelHttpClient { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }

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
            _allElements = (await FinancialLabelHttpClient.Get(0, _labelsCount)).ToList();
            ApplyFilter();
        }
        catch (Exception)
        {
            _errors.Add("Failed while getting labels");
            _isLoading = false;
            return;
        }

        _isLoading = false;
    }

    private async Task PageChanged(int i)
    {
        try
        {
            SelectedPage = i;
            _elements = PageItems(i);
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

        _elements = PageItems(SelectedPage);
    }

    private void OnSearchChanged(string value)
    {
        _searchText = value ?? string.Empty;
        SelectedPage = 1;
        ApplyFilter();
    }

    private IEnumerable<FinancialLabel> PageItems(int page) =>
        _filteredElements.Skip((page - 1) * _elementsPerPage).Take(_elementsPerPage).ToList();

    private void ApplyFilter()
    {
        _filteredElements = string.IsNullOrWhiteSpace(_searchText)
            ? _allElements
            : _allElements.Where(item => item.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)).ToList();

        _pagesCount = _filteredElements.Count == 0 ? 0 : (int)Math.Ceiling((double)_filteredElements.Count / _elementsPerPage);

        if (_pagesCount == 0)
        {
            SelectedPage = 1;
            _elements = [];
            return;
        }

        if (SelectedPage > _pagesCount)
            SelectedPage = _pagesCount;

        _elements = PageItems(SelectedPage);
    }

    private async Task LoadSuggestions()
    {
        _showSuggestions = true;
        _suggestionErrors.Clear();
        _isLoadingSuggestions = true;
        _suggestionsRan = false;

        try
        {
            _suggestions = await FinancialLabelHttpClient.GetSuggestions(_suggestionSampleSize, _maxSuggestions);
            _dismissed.Clear();
            RefreshVisibleSuggestions();
            _suggestionsRan = true;
        }
        catch (Exception ex)
        {
            _suggestionErrors.Add($"Failed to load suggestions: {ex.Message}");
        }
        finally
        {
            _isLoadingSuggestions = false;
        }
    }

    private async Task AddSuggestion(LabelSuggestion suggestion)
    {
        if (!_processing.Add(suggestion.Name)) return;

        try
        {
            var ok = await FinancialLabelHttpClient.Add(new AddFinancialLabel(suggestion.Name));
            if (ok)
            {
                Snackbar.Add($"Label '{suggestion.Name}' added.", Severity.Success);
                _dismissed.Add(suggestion.Name);
                RefreshVisibleSuggestions();
                await ReloadLabels();
            }
            else
            {
                Snackbar.Add($"Failed to add label '{suggestion.Name}'.", Severity.Error);
            }
        }
        finally
        {
            _processing.Remove(suggestion.Name);
        }
    }

    private void DismissSuggestion(LabelSuggestion suggestion)
    {
        _dismissed.Add(suggestion.Name);
        RefreshVisibleSuggestions();
    }

    private void CloseSuggestions()
    {
        _showSuggestions = false;
        _suggestionsRan = false;
        _suggestions = [];
        _visibleSuggestions = [];
        _suggestionErrors.Clear();
        _dismissed.Clear();
    }

    private void RefreshVisibleSuggestions() =>
        _visibleSuggestions = _suggestions.Where(s => !_dismissed.Contains(s.Name)).ToList();

    private async Task ReloadLabels()
    {
        try
        {
            _labelsCount = await FinancialLabelHttpClient.GetCount();
            _allElements = (await FinancialLabelHttpClient.Get(0, _labelsCount)).ToList();
            ApplyFilter();
        }
        catch
        {
            _errors.Add("Failed to refresh labels after adding a new one.");
        }
    }
}
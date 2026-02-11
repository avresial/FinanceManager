using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Stocks;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.Admin;

public partial class AdminEditStock : ComponentBase
{
    private readonly List<string> _errors = [];
    private readonly List<string> _info = [];

    private MudForm? _form;
    private bool _isValid;
    private bool _isLoading = true;

    private StockDetails? _details;
    private string _name = string.Empty;
    private string _type = string.Empty;
    private string _region = string.Empty;
    private string _currency = string.Empty;

    [Parameter] public required string Ticker { get; set; }

    [Inject] public required StockPriceHttpClient StockPriceHttpClient { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;
        _errors.Clear();

        try
        {
            _details = await StockPriceHttpClient.GetStockDetails(Ticker);
            if (_details is null)
            {
                _isLoading = false;
                return;
            }

            _name = _details.Name;
            _type = _details.Type;
            _region = _details.Region;
            _currency = _details.Currency.ShortName;
        }
        catch (Exception ex)
        {
            _errors.Add(ex.Message);
        }

        _isLoading = false;
    }

    private async Task Save()
    {
        _errors.Clear();
        _info.Clear();

        if (_details is null || _form is null)
            return;

        await _form.Validate();
        if (!_form.IsValid)
        {
            _errors.Add("Please correct the validation errors before submitting.");
            return;
        }

        try
        {
            var result = await StockPriceHttpClient.UpdateStockDetails(
                _details.Ticker,
                _name.Trim(),
                _type.Trim(),
                _region.Trim(),
                _currency.Trim());
            if (result is null)
            {
                _errors.Add("Failed to update stock details.");
                return;
            }

            _info.Add("Stock updated successfully.");
            NavigationManager.NavigateTo("Admin/Stocks");
        }
        catch (Exception ex)
        {
            _errors.Add(ex.Message);
        }
    }

    private void Cancel()
    {
        NavigationManager.NavigateTo("Admin/Stocks");
    }
}

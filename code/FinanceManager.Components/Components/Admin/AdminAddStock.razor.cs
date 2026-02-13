using FinanceManager.Components.HttpClients;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.Admin;

public partial class AdminAddStock : ComponentBase
{
    private readonly List<string> _errors = [];
    private readonly List<string> _info = [];

    private MudForm? _form;
    private bool _isValid;

    private string _ticker = string.Empty;
    private string _name = string.Empty;
    private string _type = string.Empty;
    private string _region = string.Empty;
    private string _currency = string.Empty;

    [Inject] public required StockPriceHttpClient StockPriceHttpClient { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }

    private async Task AddStock()
    {
        _errors.Clear();
        _info.Clear();

        if (_form is null)
        {
            _errors.Add("Form initialization error. Please try again.");
            return;
        }

        await _form.Validate();
        if (!_form.IsValid)
        {
            _errors.Add("Please correct the validation errors before submitting.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_ticker))
        {
            _errors.Add("Ticker is required.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_name) || string.IsNullOrWhiteSpace(_type) || string.IsNullOrWhiteSpace(_region))
        {
            _errors.Add("Name, type, and region are required.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_currency))
        {
            _errors.Add("Currency is required.");
            return;
        }

        try
        {
            var result = await StockPriceHttpClient.AddStockDetails(
                _ticker.Trim(),
                _name.Trim(),
                _type.Trim(),
                _region.Trim(),
                _currency.Trim());
            if (result is null)
            {
                _errors.Add("Failed to add stock details.");
                return;
            }

            _info.Add("Stock added successfully.");
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
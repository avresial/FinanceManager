using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FinanceManager.Components.Components.Admin;

public partial class AdminBulkStockPricesImport : ComponentBase
{
    private readonly List<string> _errors = [];

    private IBrowserFile? _selectedFile;
    private string? _selectedFileName;
    private bool _isImporting;
    private StockPriceBulkImportResultDto? _result;

    [Inject] public required StockPriceHttpClient StockPriceHttpClient { get; set; }

    private async Task UploadFile(InputFileChangeEventArgs eventArgs)
    {
        _errors.Clear();
        _result = null;

        var file = eventArgs.File;
        if (file is null)
        {
            _selectedFile = null;
            _selectedFileName = null;
            _errors.Add("No file selected.");
            return;
        }

        if (!Path.GetExtension(file.Name).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            _selectedFile = null;
            _selectedFileName = null;
            _errors.Add("Only .csv files are supported.");
            return;
        }

        _selectedFile = file;
        _selectedFileName = file.Name;

        await Task.CompletedTask;
    }

    private async Task BeginImport()
    {
        _errors.Clear();
        _result = null;

        if (_selectedFile is null)
        {
            _errors.Add("Select a CSV file first.");
            return;
        }

        _isImporting = true;
        try
        {
            _result = await StockPriceHttpClient.BulkImportClosePrices(_selectedFile);
            if (_result is null)
                _errors.Add("Import completed but no summary was returned.");
        }
        catch (Exception ex)
        {
            _errors.Add(ex.Message);
        }
        finally
        {
            _isImporting = false;
        }
    }

    private void ClearSelection()
    {
        _selectedFile = null;
        _selectedFileName = null;
        _result = null;
        _errors.Clear();
    }
}
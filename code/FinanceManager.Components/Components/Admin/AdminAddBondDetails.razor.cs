using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Enums;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.Admin;

public partial class AdminAddBondDetails : ComponentBase
{
    private readonly List<string> _errors = [];
    private readonly List<string> _info = [];

    private MudForm? _form;
    private bool _isValid;

    private string _name = string.Empty;
    private string _issuer = string.Empty;
    private DateTime? _startDate = DateTime.Today;
    private DateTime? _endDate = DateTime.Today.AddYears(1);

    private BondType _selectedType = BondType.InflationBond;
    private Currency _selectedCurrency = DefaultCurrency.PLN;
    private Capitalization _selectedCapitalization = BondDetails.Capitalization;

    private DateOperator _methodDateOperator = DateOperator.UntilDate;
    private DateTime? _methodDate = DateTime.Today;
    private decimal? _methodRate;

    private IReadOnlyList<string> _issuers = [];

    private readonly List<BondCalculationMethod> _calculationMethods = [];
    private readonly IReadOnlyList<BondType> _bondTypes = Enum.GetValues<BondType>();
    private readonly IReadOnlyList<Currency> _currencies = [DefaultCurrency.PLN, DefaultCurrency.USD];
    private readonly IReadOnlyList<Capitalization> _capitalizations = Enum.GetValues<Capitalization>();
    private readonly IReadOnlyList<DateOperator> _dateOperators = Enum.GetValues<DateOperator>();

    [Inject] public required BondDetailsHttpClient BondDetailsHttpClient { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _issuers = await BondDetailsHttpClient.GetIssuers();
        }
        catch (Exception ex)
        {
            _errors.Add(ex.Message);
        }
    }

    private async Task AddBondDetails()
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

        if (string.IsNullOrWhiteSpace(_name) || string.IsNullOrWhiteSpace(_issuer))
        {
            _errors.Add("Name and issuer are required.");
            return;
        }

        if (!_startDate.HasValue || !_endDate.HasValue)
        {
            _errors.Add("Start and end emission dates are required.");
            return;
        }

        if (_endDate.Value.Date < _startDate.Value.Date)
        {
            _errors.Add("End emission date must be after start emission date.");
            return;
        }

        if (_calculationMethods.Count == 0)
        {
            _errors.Add("At least one calculation method is required.");
            return;
        }

        var bond = new BondDetails
        {
            Name = _name.Trim(),
            Issuer = _issuer.Trim(),
            StartEmissionDate = DateOnly.FromDateTime(_startDate.Value),
            EndEmissionDate = DateOnly.FromDateTime(_endDate.Value),
            Type = _selectedType,
            Currency = _selectedCurrency,
            CalculationMethods = _calculationMethods.ToList(),
        };

        try
        {
            var result = await BondDetailsHttpClient.Add(bond);
            if (result is null)
            {
                _errors.Add("Failed to add bond details.");
                return;
            }

            _info.Add("Bond details added successfully.");
            NavigationManager.NavigateTo("Admin/Bonds");
        }
        catch (Exception ex)
        {
            _errors.Add(ex.Message);
        }
    }

    private void AddCalculationMethod()
    {
        if (!_methodDate.HasValue)
        {
            _errors.Add("Calculation method date is required.");
            return;
        }

        if (!_methodRate.HasValue || _methodRate.Value <= 0)
        {
            _errors.Add("Calculation method rate is required.");
            return;
        }

        _calculationMethods.Add(new BondCalculationMethod
        {
            DateOperator = _methodDateOperator,
            DateValue = _methodDate.Value.ToString("yyyy-MM-dd"),
            Rate = _methodRate.Value,
        });

        _methodRate = null;
    }

    private Task<IEnumerable<string>> SearchIssuer(string value, CancellationToken cancellationToken)
    {
        if (_issuers.Count == 0)
        {
            return Task.FromResult(Enumerable.Empty<string>());
        }

        var query = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(_issuers.AsEnumerable());
        }

        var ranked = _issuers
            .Select(issuer => new { Issuer = issuer, Score = GetIssuerScore(issuer, query) })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Issuer, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Issuer)
            .Take(8);

        return Task.FromResult(ranked);
    }

    private static int GetIssuerScore(string issuer, string query)
    {
        var issuerLower = issuer.ToLowerInvariant();
        var queryLower = query.ToLowerInvariant();

        if (issuerLower == queryLower)
        {
            return 10000;
        }

        if (issuerLower.StartsWith(queryLower, StringComparison.Ordinal))
        {
            return 8000 - (issuerLower.Length - queryLower.Length);
        }

        if (issuerLower.Contains(queryLower, StringComparison.Ordinal))
        {
            return 6000 - (issuerLower.Length - queryLower.Length);
        }

        var distance = GetLevenshteinDistance(issuerLower, queryLower);
        return 1000 - distance;
    }

    private static int GetLevenshteinDistance(string source, string target)
    {
        if (source.Length == 0) return target.Length;
        if (target.Length == 0) return source.Length;

        var costs = new int[target.Length + 1];
        for (var i = 0; i <= target.Length; i++)
        {
            costs[i] = i;
        }

        for (var i = 1; i <= source.Length; i++)
        {
            var previousCost = i - 1;
            costs[0] = i;

            for (var j = 1; j <= target.Length; j++)
            {
                var currentCost = costs[j];
                var substitutionCost = source[i - 1] == target[j - 1] ? 0 : 1;
                costs[j] = Math.Min(
                    Math.Min(costs[j] + 1, costs[j - 1] + 1),
                    previousCost + substitutionCost);
                previousCost = currentCost;
            }
        }

        return costs[target.Length];
    }

    private void RemoveCalculationMethod(BondCalculationMethod method)
    {
        _calculationMethods.Remove(method);
    }

    private void Cancel()
    {
        NavigationManager.NavigateTo("Admin/Bonds");
    }
}
using FinanceManager.Application.Commands.Bonds;
using FinanceManager.Domain.Entities.Bonds;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class BondDetailsHttpClient(HttpClient httpClient)
{
    public Task<BondDetails?> GetById(int id, CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<BondDetails>($"{httpClient.BaseAddress}api/BondDetails/{id}", cancellationToken);

    public async Task<List<BondDetails>> GetAll(CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<List<BondDetails>>($"{httpClient.BaseAddress}api/BondDetails", cancellationToken);
        return result ?? [];
    }

    public async Task<List<BondDetails>> GetByIssuer(string issuer, CancellationToken cancellationToken = default)
    {
        var encoded = Uri.EscapeDataString(issuer);
        var result = await httpClient.GetFromJsonAsync<List<BondDetails>>($"{httpClient.BaseAddress}api/BondDetails/by-issuer/{encoded}", cancellationToken);
        return result ?? [];
    }

    public async Task<BondDetails?> Add(BondDetails bond, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/BondDetails", bond, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BondDetails>(cancellationToken: cancellationToken);
    }

    public async Task<bool> Update(UpdateBondDetails updateBondDetails, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/BondDetails", updateBondDetails, cancellationToken);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating bond details: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> Delete(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.DeleteAsync($"{httpClient.BaseAddress}api/BondDetails/{id}", cancellationToken);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> AddCalculationMethod(int bondDetailsId, BondCalculationMethod calculationMethod, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/BondDetails/{bondDetailsId}/calculation-methods", calculationMethod, cancellationToken);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RemoveCalculationMethod(int bondDetailsId, int calculationMethodId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.DeleteAsync($"{httpClient.BaseAddress}api/BondDetails/{bondDetailsId}/calculation-methods/{calculationMethodId}", cancellationToken);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
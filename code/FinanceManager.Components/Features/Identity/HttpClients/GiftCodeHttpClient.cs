using FinanceManager.Domain.Identity.GiftCodes;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.Identity.HttpClients;

public class GiftCodeHttpClient(HttpClient httpClient)
{
    private string BaseUrl => $"{httpClient.BaseAddress}api/gift-codes";
    private string AdminUrl => $"{httpClient.BaseAddress}api/admin/gift-codes";

    public Task<List<GiftCodeDto>?> GetAdminGiftCodes(int offset, int count) =>
        httpClient.GetFromJsonAsync<List<GiftCodeDto>>($"{AdminUrl}?offset={offset}&count={count}");

    public async Task<GeneratedGiftCode?> Generate(GenerateGiftCode request)
    {
        using var response = await httpClient.PostAsJsonAsync(AdminUrl, request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GeneratedGiftCode>();
    }

    public Task<HttpResponseMessage> Revoke(long id) =>
        httpClient.PostAsync($"{AdminUrl}/{id}/revoke", content: null);

    public Task<HttpResponseMessage> Redeem(string code) =>
        httpClient.PostAsJsonAsync($"{BaseUrl}/redeem", new RedeemGiftCode(code));
}
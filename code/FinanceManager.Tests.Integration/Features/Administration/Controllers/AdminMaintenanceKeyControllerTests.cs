using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Tests.Integration.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Administration.Controllers;

[Trait("Category", "Integration")]
public class AdminMaintenanceKeyControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private sealed record StatusResponse(bool HasKey, DateTimeOffset? CreatedAt);
    private sealed record GeneratedResponse(string Key, DateTimeOffset CreatedAt);

    [Fact]
    public async Task Endpoints_WithoutAuthentication_ReturnUnauthorized()
    {
        var response = await Client.GetAsync("api/admin/maintenance-key", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Endpoints_AsNonAdmin_ReturnForbidden()
    {
        Authorize("user", 2, UserRole.User);

        var response = await Client.PostAsync("api/admin/maintenance-key", content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GenerateRollRevoke_FullLifecycle_WorksEndToEnd()
    {
        Authorize("admin", 1, UserRole.Admin);
        var ct = TestContext.Current.CancellationToken;

        // Initially no key.
        var status = await Client.GetFromJsonAsync<StatusResponse>("api/admin/maintenance-key", ct);
        Assert.NotNull(status);
        Assert.False(status.HasKey);

        // Generate: plaintext returned once, status flips to active.
        var generateResponse = await Client.PostAsync("api/admin/maintenance-key", content: null, ct);
        Assert.Equal(HttpStatusCode.OK, generateResponse.StatusCode);
        var generated = await generateResponse.Content.ReadFromJsonAsync<GeneratedResponse>(ct);
        Assert.NotNull(generated);
        Assert.StartsWith("fmk_", generated.Key);

        status = await Client.GetFromJsonAsync<StatusResponse>("api/admin/maintenance-key", ct);
        Assert.NotNull(status);
        Assert.True(status.HasKey);

        // The generated key authorises the maintenance endpoint end-to-end.
        using (var trigger = new HttpRequestMessage(HttpMethod.Post, "api/PriceBackfill"))
        {
            trigger.Headers.Add("X-Maintenance-Key", generated.Key);
            var triggerResponse = await Client.SendAsync(trigger, ct);
            Assert.Equal(HttpStatusCode.Accepted, triggerResponse.StatusCode);
        }

        // Roll: a new key replaces the old one, which stops working.
        var rollResponse = await Client.PostAsync("api/admin/maintenance-key", content: null, ct);
        var rolled = await rollResponse.Content.ReadFromJsonAsync<GeneratedResponse>(ct);
        Assert.NotNull(rolled);
        Assert.NotEqual(generated.Key, rolled.Key);

        using (var trigger = new HttpRequestMessage(HttpMethod.Post, "api/PriceBackfill"))
        {
            trigger.Headers.Add("X-Maintenance-Key", generated.Key);
            var triggerResponse = await Client.SendAsync(trigger, ct);
            Assert.Equal(HttpStatusCode.Unauthorized, triggerResponse.StatusCode);
        }

        // Revoke: key gone, second revoke reports nothing to do.
        var revokeResponse = await Client.DeleteAsync("api/admin/maintenance-key", ct);
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        var secondRevoke = await Client.DeleteAsync("api/admin/maintenance-key", ct);
        Assert.Equal(HttpStatusCode.NotFound, secondRevoke.StatusCode);
    }
}
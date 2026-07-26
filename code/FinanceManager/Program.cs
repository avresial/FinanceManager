using Blazored.LocalStorage;
using Blazored.SessionStorage;
using FinanceManager.Application;
using FinanceManager.Components;
using FinanceManager.Components.Shared.Services;
using FinanceManager.WebUi;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore();
builder.Services.AddMudServices();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddBlazoredSessionStorage();

builder.Services.AddTransient<TokenRefreshRedirectHandler>();
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<TokenRefreshRedirectHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler) { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
});

builder.Services.AddApplication().AddUIComponents();

await builder.Build().RunAsync();
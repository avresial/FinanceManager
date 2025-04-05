using Blazored.LocalStorage;
using Blazored.SessionStorage;
using FinanceManager.Application;
using FinanceManager.Components;
using FinanceManager.Infrastructure;
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
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://localhost:7060/") });
builder.Services.AddApplication().AddInfrastructureFrontend().AddUIComponents();

await builder.Build().RunAsync();

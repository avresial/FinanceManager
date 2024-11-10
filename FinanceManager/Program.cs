using Blazored.LocalStorage;
using Blazored.SessionStorage;
using FinanceManager;
using FinanceManager.Application;
using FinanceManager.Infrastructure;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

//builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddMudServices();
builder.Services.AddApplication().AddInfrastructure();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddBlazoredSessionStorage();

//builder.Services.AddOidcAuthentication(options =>
//{
//	// Configure your authentication provider options here.
//	// For more information, see https://aka.ms/blazor-standalone-auth
//	builder.Configuration.Bind("Local", options.ProviderOptions);
//});

await builder.Build().RunAsync();

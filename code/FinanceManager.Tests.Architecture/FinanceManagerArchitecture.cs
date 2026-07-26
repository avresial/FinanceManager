using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using FinanceManager.Application.Administration.Users;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

namespace FinanceManager.Tests.Architecture;

/// <summary>
/// Loads the FinanceManager assemblies once and exposes each architectural layer as a
/// reusable <see cref="IObjectProvider{IType}"/> so the layer-leakage rules read declaratively.
/// </summary>
/// <remarks>
/// Layers are defined by <b>assembly</b> (the real module/project boundary), not by namespace.
/// Some types in this codebase are physically declared in one project but carry another
/// project's namespace (e.g. DTOs in the Domain/Components projects sit under
/// <c>FinanceManager.Infrastructure.Dtos</c>), so a namespace-based definition would
/// misclassify them. Assembly-based definitions reflect the actual reference graph.
/// </remarks>
public static class FinanceManagerArchitecture
{
    // One marker type per project: locates the assembly and forces the compiler to keep the
    // project reference (so the assembly is copied to the output dir for the loader).
    private static readonly System.Reflection.Assembly _domainAssembly = typeof(FinanceManager.Domain.Administration.Logging.LogEntry).Assembly;
    private static readonly System.Reflection.Assembly _applicationAssembly = typeof(FinanceManager.Application.Administration.Users.AdministrationUsersService).Assembly;
    private static readonly System.Reflection.Assembly _infrastructureAssembly = typeof(FinanceManager.Infrastructure.Features.FinancialAccounts.Shared.Repositories.AccountRepository).Assembly;
    private static readonly System.Reflection.Assembly _apiAssembly = typeof(FinanceManager.Api.Features.MoneyFlow.Controllers.AssetsController).Assembly;
    private static readonly System.Reflection.Assembly _componentsAssembly = typeof(FinanceManager.Components.HttpClients.AdminAiProvidersHttpClient).Assembly;
    private static readonly System.Reflection.Assembly _webUiAssembly = typeof(FinanceManager.WebUi.App).Assembly;

    public static readonly ArchitectureModel LoadedArchitecture = new ArchLoader()
        .LoadAssemblies(
            _domainAssembly,
            _applicationAssembly,
            _infrastructureAssembly,
            _apiAssembly,
            _componentsAssembly,
            _webUiAssembly)
        .Build();

    public static readonly IObjectProvider<IType> DomainLayer =
        Types().That().ResideInAssembly(_domainAssembly).As("Domain layer");

    public static readonly IObjectProvider<IType> ApplicationLayer =
        Types().That().ResideInAssembly(_applicationAssembly).As("Application layer");

    public static readonly IObjectProvider<IType> InfrastructureLayer =
        Types().That().ResideInAssembly(_infrastructureAssembly).As("Infrastructure layer");

    public static readonly IObjectProvider<IType> ApiLayer =
        Types().That().ResideInAssembly(_apiAssembly).As("Api layer");

    public static readonly IObjectProvider<IType> ComponentsLayer =
        Types().That().ResideInAssembly(_componentsAssembly).As("Components layer");

    // FinanceManager.WebUi is the Blazor WASM bootstrap (Program.cs, App). It runs in the
    // browser, so it is subject to the same constraints as Components.
    public static readonly IObjectProvider<IType> WebUiLayer =
        Types().That().ResideInAssembly(_webUiAssembly).As("WASM host (FinanceManager.WebUi)");

    // The full browser-side surface: Blazor Components plus the WASM bootstrap host.
    public static readonly IObjectProvider<IType> BrowserSideLayer =
        Types().That().ResideInAssembly(_componentsAssembly, _webUiAssembly).As("Browser-side (Components + WASM host)");

    // External framework namespaces the inner layers must stay clear of. These live in
    // assemblies we deliberately do NOT load, so they are matched as dependency *targets*
    // (NotDependOnAnyTypesThat().ResideInNamespaceMatching(...)) rather than as loaded types.
    // An IObjectProvider over Types() would be empty here — only the five FinanceManager
    // assemblies are loaded — and the rule would pass vacuously.
    public const string EntityFrameworkCoreNamespacePattern = @"^Microsoft\.EntityFrameworkCore";

    public const string AspNetCoreNamespacePattern = @"^Microsoft\.AspNetCore";
}
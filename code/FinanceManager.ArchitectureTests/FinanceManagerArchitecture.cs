using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace FinanceManager.ArchitectureTests;

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
    private static readonly System.Reflection.Assembly _domainAssembly = typeof(FinanceManager.Domain.Entities.Logging.LogEntry).Assembly;
    private static readonly System.Reflection.Assembly _applicationAssembly = typeof(FinanceManager.Application.Services.AdministrationUsersService).Assembly;
    private static readonly System.Reflection.Assembly _infrastructureAssembly = typeof(FinanceManager.Infrastructure.Repositories.AccountRepository).Assembly;
    private static readonly System.Reflection.Assembly _apiAssembly = typeof(FinanceManager.Api.Controllers.AssetsController).Assembly;
    private static readonly System.Reflection.Assembly _componentsAssembly = typeof(FinanceManager.Components.HttpClients.AdminAiProvidersHttpClient).Assembly;

    public static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            _domainAssembly,
            _applicationAssembly,
            _infrastructureAssembly,
            _apiAssembly,
            _componentsAssembly)
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

    // External infrastructure concerns the inner layers must stay clear of. These live in
    // their own assemblies, so namespace matching is the right tool here.
    public static readonly IObjectProvider<IType> EntityFrameworkCore =
        Types().That().ResideInNamespaceMatching(@"^Microsoft\.EntityFrameworkCore").As("Entity Framework Core");

    public static readonly IObjectProvider<IType> AspNetCore =
        Types().That().ResideInNamespaceMatching(@"^Microsoft\.AspNetCore").As("ASP.NET Core");
}
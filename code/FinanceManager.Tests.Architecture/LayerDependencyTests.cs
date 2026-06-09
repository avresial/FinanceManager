using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static FinanceManager.Tests.Architecture.FinanceManagerArchitecture;

namespace FinanceManager.Tests.Architecture;

/// <summary>
/// Enforces the layered modular monolith described in CLAUDE.md:
/// Domain ← Application ← Infrastructure ← Api, with the browser-side surface
/// (Components + the FinanceManager.WebUi WASM host) ← Application.
/// Dependencies only ever point downward; the Domain stays free of infrastructure concerns
/// and the browser-side code never reaches the database.
/// </summary>
[Trait("Category", "Architecture")]
public class LayerDependencyTests
{
    // --- Domain is the pure leaf -------------------------------------------------------------

    [Fact]
    public void Domain_should_not_depend_on_any_other_layer()
    {
        IArchRule rule = Types().That().Are(DomainLayer)
            .Should().NotDependOnAny(ApplicationLayer)
            .AndShould().NotDependOnAny(InfrastructureLayer)
            .AndShould().NotDependOnAny(ApiLayer)
            .AndShould().NotDependOnAny(ComponentsLayer)
            .AndShould().NotDependOnAny(WebUiLayer)
            .Because("the Domain layer is the leaf of the dependency graph and must not reference outer layers.");

        rule.Check(LoadedArchitecture);
    }

    [Fact]
    public void Domain_should_not_depend_on_entity_framework_core()
    {
        IArchRule rule = Types().That().Are(DomainLayer)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(EntityFrameworkCoreNamespacePattern)
            .Because("the Domain layer must remain persistence-ignorant (no EF Core).");

        rule.Check(LoadedArchitecture);
    }

    [Fact]
    public void Domain_should_not_depend_on_aspnet_core()
    {
        IArchRule rule = Types().That().Are(DomainLayer)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(AspNetCoreNamespacePattern)
            .Because("the Domain layer must not reference ASP.NET Core.");

        rule.Check(LoadedArchitecture);
    }

    // --- Application sits directly above Domain ----------------------------------------------

    [Fact]
    public void Application_should_not_depend_on_outer_layers()
    {
        IArchRule rule = Types().That().Are(ApplicationLayer)
            .Should().NotDependOnAny(InfrastructureLayer)
            .AndShould().NotDependOnAny(ApiLayer)
            .AndShould().NotDependOnAny(ComponentsLayer)
            .AndShould().NotDependOnAny(WebUiLayer)
            .Because("the Application layer orchestrates use cases and must not depend on Infrastructure or the presentation layers.");

        rule.Check(LoadedArchitecture);
    }

    // --- Infrastructure may know Application/Domain but nothing above it ----------------------

    [Fact]
    public void Infrastructure_should_not_depend_on_presentation_layers()
    {
        IArchRule rule = Types().That().Are(InfrastructureLayer)
            .Should().NotDependOnAny(ApiLayer)
            .AndShould().NotDependOnAny(ComponentsLayer)
            .AndShould().NotDependOnAny(WebUiLayer)
            .Because("Infrastructure is an inner layer and must not depend on the Api or browser-side presentation layers.");

        rule.Check(LoadedArchitecture);
    }

    // --- Browser-side (Components + WASM host) must never touch the database ------------------

    [Fact]
    public void Browser_side_should_not_depend_on_infrastructure()
    {
        IArchRule rule = Types().That().Are(BrowserSideLayer)
            .Should().NotDependOnAny(InfrastructureLayer)
            .Because("browser-side code must reach the server through typed HttpClients, never the Infrastructure layer directly.");

        rule.Check(LoadedArchitecture);
    }

    [Fact]
    public void Browser_side_should_not_depend_on_entity_framework_core()
    {
        IArchRule rule = Types().That().Are(BrowserSideLayer)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(EntityFrameworkCoreNamespacePattern)
            .Because("browser-side code runs in the browser and must never access the database (no EF Core).");

        rule.Check(LoadedArchitecture);
    }

    [Fact]
    public void Browser_side_should_not_depend_on_api()
    {
        IArchRule rule = Types().That().Are(BrowserSideLayer)
            .Should().NotDependOnAny(ApiLayer)
            .Because("browser-side code talks to the Api over HTTP, not by referencing the Api host assembly.");

        rule.Check(LoadedArchitecture);
    }
}
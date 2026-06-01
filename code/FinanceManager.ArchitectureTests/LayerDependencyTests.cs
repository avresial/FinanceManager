using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static FinanceManager.ArchitectureTests.FinanceManagerArchitecture;

namespace FinanceManager.ArchitectureTests;

/// <summary>
/// Enforces the layered modular monolith described in CLAUDE.md:
/// Domain ← Application ← Infrastructure ← Api, with Components ← Application.
/// Dependencies only ever point downward; the Domain stays free of infrastructure concerns
/// and the browser-side Components never reach the database.
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
            .Because("the Domain layer is the leaf of the dependency graph and must not reference outer layers.");

        rule.Check(Architecture);
    }

    [Fact]
    public void Domain_should_not_depend_on_entity_framework_core()
    {
        IArchRule rule = Types().That().Are(DomainLayer)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(EntityFrameworkCoreNamespacePattern)
            .Because("the Domain layer must remain persistence-ignorant (no EF Core).");

        rule.Check(Architecture);
    }

    [Fact]
    public void Domain_should_not_depend_on_aspnet_core()
    {
        IArchRule rule = Types().That().Are(DomainLayer)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(AspNetCoreNamespacePattern)
            .Because("the Domain layer must not reference ASP.NET Core.");

        rule.Check(Architecture);
    }

    // --- Application sits directly above Domain ----------------------------------------------

    [Fact]
    public void Application_should_not_depend_on_outer_layers()
    {
        IArchRule rule = Types().That().Are(ApplicationLayer)
            .Should().NotDependOnAny(InfrastructureLayer)
            .AndShould().NotDependOnAny(ApiLayer)
            .AndShould().NotDependOnAny(ComponentsLayer)
            .Because("the Application layer orchestrates use cases and must not depend on Infrastructure, Api or Components.");

        rule.Check(Architecture);
    }

    // --- Infrastructure may know Application/Domain but nothing above it ----------------------

    [Fact]
    public void Infrastructure_should_not_depend_on_presentation_layers()
    {
        IArchRule rule = Types().That().Are(InfrastructureLayer)
            .Should().NotDependOnAny(ApiLayer)
            .AndShould().NotDependOnAny(ComponentsLayer)
            .Because("Infrastructure is an inner layer and must not depend on the Api or Components presentation layers.");

        rule.Check(Architecture);
    }

    // --- Components is browser-side and must never touch the database -------------------------

    [Fact]
    public void Components_should_not_depend_on_infrastructure()
    {
        IArchRule rule = Types().That().Are(ComponentsLayer)
            .Should().NotDependOnAny(InfrastructureLayer)
            .Because("Blazor Components must reach the server through typed HttpClients, never the Infrastructure layer directly.");

        rule.Check(Architecture);
    }

    [Fact]
    public void Components_should_not_depend_on_entity_framework_core()
    {
        IArchRule rule = Types().That().Are(ComponentsLayer)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(EntityFrameworkCoreNamespacePattern)
            .Because("Blazor Components run in the browser and must never access the database (no EF Core).");

        rule.Check(Architecture);
    }

    [Fact]
    public void Components_should_not_depend_on_api()
    {
        IArchRule rule = Types().That().Are(ComponentsLayer)
            .Should().NotDependOnAny(ApiLayer)
            .Because("Components talk to the Api over HTTP, not by referencing the Api host assembly.");

        rule.Check(Architecture);
    }
}
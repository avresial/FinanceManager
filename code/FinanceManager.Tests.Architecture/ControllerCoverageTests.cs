using FinanceManager.Api.Features.MoneyFlow.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Tests.Architecture;

[Trait("Category", "Architecture")]
public class ControllerCoverageTests
{
    [Fact]
    public void EveryApiControllerHasIntegrationTests()
    {
        var apiControllerNames = typeof(AssetsController).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true }
                && typeof(ControllerBase).IsAssignableFrom(type)
                && type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var integrationTestsPath = GetRepositoryPath("code", "FinanceManager.Tests.Integration", "Features");
        var testedControllerNames = Directory
            .EnumerateFiles(integrationTestsPath, "*ControllerTests.cs", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null)
            .Select(name => name![..^"Tests".Length])
            .ToHashSet(StringComparer.Ordinal);

        var missingTests = apiControllerNames
            .Where(controllerName => !testedControllerNames.Contains(controllerName))
            .ToArray();

        Assert.True(
            missingTests.Length == 0,
            $"Missing integration controller tests for: {string.Join(", ", missingTests)}");
    }

    private static string GetRepositoryPath(params string[] pathParts)
    {
        foreach (var startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var current = new DirectoryInfo(startPath);
            while (current is not null)
            {
                var candidate = Path.Combine([current.FullName, .. pathParts]);
                if (Directory.Exists(candidate))
                    return candidate;

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException($"Could not find repository path: {Path.Combine(pathParts)}");
    }
}
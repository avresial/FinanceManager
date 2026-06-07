using FinanceManager.Components;
using System.Reflection;

namespace FinanceManager.Tests.Unit.Components;

[Trait("Category", "Unit")]
public class ApplicationVersionTests
{
    [Fact]
    public void Version_MatchesCalVerFormat()
    {
        Assert.Matches(@"^\d{2}\.\d{2}\.\d{2}$", ApplicationVersion.Version);
    }

    [Fact]
    public void Version_ReflectsInformationalVersionAttribute()
    {
        var attribute = typeof(ApplicationVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        Assert.NotNull(attribute);

        var expected = attribute!.InformationalVersion;
        var plus = expected.IndexOf('+');
        if (plus >= 0) expected = expected[..plus];

        Assert.Equal(expected, ApplicationVersion.Version);
    }
}
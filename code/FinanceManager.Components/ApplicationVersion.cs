using System.Reflection;

namespace FinanceManager.Components;

public static class ApplicationVersion
{
    public static string Version { get; } = ReadInformationalVersion();

    private static string ReadInformationalVersion()
    {
        var attribute = typeof(ApplicationVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        if (attribute is null) return string.Empty;

        // Strip the build metadata suffix MSBuild appends (e.g. "26.05.25+abc1234").
        var raw = attribute.InformationalVersion;
        var plus = raw.IndexOf('+');
        return plus >= 0 ? raw[..plus] : raw;
    }
}
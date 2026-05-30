using System.Runtime.CompilerServices;

namespace FinanceManager.IntegrationTests;

internal static class TestEnvironment
{
    // The JWT signing key is intentionally absent from appsettings.test.json (#273). The integration
    // host runs in the non-Development "test" environment, where the startup guard requires a key to
    // be supplied via environment variable / User Secrets. Set a deterministic, non-secret test key
    // before any test runs so both the API host and the OptionsProvider-based token generator agree.
    [ModuleInitializer]
    internal static void SetJwtSigningKey()
        => Environment.SetEnvironmentVariable("JwtConfig__Key",
            "integration-test-only-insecure-jwt-signing-key-do-not-use-anywhere");
}
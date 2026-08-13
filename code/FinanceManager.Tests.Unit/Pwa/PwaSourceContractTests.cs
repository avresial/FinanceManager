using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace FinanceManager.Tests.Unit.Pwa;

public class PwaSourceContractTests
{
    [Fact]
    public void ManifestLinkageAndRequiredProperties_InHtmlAndManifestFile()
    {
        string wwwroot = GetWwwRootDirectory();
        string indexHtmlPath = Path.Combine(wwwroot, "index.html");
        Assert.True(File.Exists(indexHtmlPath), $"index.html must exist at {indexHtmlPath}");

        string indexHtmlContent = File.ReadAllText(indexHtmlPath);

        Match manifestLink = Regex.Match(
            indexHtmlContent,
            @"<link\s+[^>]*rel=[""']manifest[""'][^>]*href=[""'](?<href>[^""']+)[""']",
            RegexOptions.IgnoreCase);
        if (!manifestLink.Success)
        {
            manifestLink = Regex.Match(
                indexHtmlContent,
                @"<link\s+[^>]*href=[""'](?<href>[^""']+)[""'][^>]*rel=[""']manifest[""']",
                RegexOptions.IgnoreCase);
        }

        Assert.True(
            manifestLink.Success,
            "index.html must contain a <link rel=\"manifest\" href=\"...\"> tag to be a valid PWA.");

        Uri manifestUri = new(new Uri("https://localhost/"), manifestLink.Groups["href"].Value);
        Assert.Equal("localhost", manifestUri.Host);

        string manifestPath = Path.Combine(
            wwwroot,
            Uri.UnescapeDataString(manifestUri.AbsolutePath).TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        Assert.True(
            File.Exists(manifestPath),
            $"The web app manifest referenced by index.html must exist at {manifestPath}.");

        string manifestJson = File.ReadAllText(manifestPath);
        using JsonDocument doc = JsonDocument.Parse(manifestJson);
        JsonElement root = doc.RootElement;

        Assert.True(
            (root.TryGetProperty("name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString())) ||
            (root.TryGetProperty("short_name", out var shortNameProp) && !string.IsNullOrWhiteSpace(shortNameProp.GetString())),
            "Manifest must contain a non-empty 'name' or 'short_name'.");

        Assert.True(
            root.TryGetProperty("start_url", out var startUrlProp) && !string.IsNullOrWhiteSpace(startUrlProp.GetString()),
            "Manifest must contain a non-empty 'start_url'.");

        Assert.True(
            root.TryGetProperty("display", out var displayProp) && !string.IsNullOrWhiteSpace(displayProp.GetString()),
            "Manifest must contain a non-empty 'display' mode.");

        Assert.True(
            (root.TryGetProperty("background_color", out var bgProp) && !string.IsNullOrWhiteSpace(bgProp.GetString())) ||
            (root.TryGetProperty("theme_color", out var themeProp) && !string.IsNullOrWhiteSpace(themeProp.GetString())),
            "Manifest must contain a 'background_color' or 'theme_color'.");

        Assert.True(
            root.TryGetProperty("icons", out var iconsProp) && iconsProp.ValueKind == JsonValueKind.Array && iconsProp.GetArrayLength() > 0,
            "Manifest must declare an 'icons' array with at least one icon entry.");

        string[] iconSizes = iconsProp.EnumerateArray()
            .Select(icon => icon.GetProperty("sizes").GetString())
            .OfType<string>()
            .ToArray();

        Assert.Contains("192x192", iconSizes);
        Assert.Contains("512x512", iconSizes);

        foreach (JsonElement icon in iconsProp.EnumerateArray())
        {
            string iconPath = icon.GetProperty("src").GetString()!;
            Assert.True(File.Exists(Path.Combine(wwwroot, iconPath)), $"Manifest icon must exist: {iconPath}");
        }
    }

    [Fact]
    public void DevelopmentVsPublishedWorkerDistinction_InServiceWorkerFilesAndProjectConfig()
    {
        string wwwroot = GetWwwRootDirectory();
        string devSwPath = Path.Combine(wwwroot, "service-worker.js");
        string prodSwPath = Path.Combine(wwwroot, "service-worker.published.js");
        string csprojPath = GetWebUiCsprojPath();

        Assert.True(File.Exists(devSwPath), $"Dev service worker must exist at {devSwPath}");
        Assert.True(File.Exists(prodSwPath), $"Published service worker must exist at {prodSwPath}");
        Assert.True(File.Exists(csprojPath), $"FinanceManager.WebUi.csproj must exist at {csprojPath}");

        string devSwContent = File.ReadAllText(devSwPath);
        string prodSwContent = File.ReadAllText(prodSwPath);
        string csprojContent = File.ReadAllText(csprojPath);

        // Contract 2a: Dev worker must NOT import asset manifest or enable offline precaching
        Assert.DoesNotContain("importScripts", devSwContent);
        Assert.DoesNotContain("service-worker-assets.js", devSwContent);
        Assert.DoesNotContain("caches.open", devSwContent);

        // Contract 2b: Dev worker should register a passthrough fetch listener or empty handler
        Assert.Contains("self.addEventListener('fetch'", devSwContent);

        // Contract 2c: Published worker MUST import service-worker-assets.js
        Assert.Contains("service-worker-assets.js", prodSwContent);
        Assert.Contains("importScripts", prodSwContent);

        // Contract 2d: Published worker MUST listen for install, activate, and fetch lifecycle events
        Assert.Contains("self.addEventListener('install'", prodSwContent);
        Assert.Contains("self.addEventListener('activate'", prodSwContent);
        Assert.Contains("self.addEventListener('fetch'", prodSwContent);

        // Contract 2e: Project file must bind dev worker to PublishedContent="wwwroot\service-worker.published.js"
        Assert.Contains("<ServiceWorker Include=\"wwwroot\\service-worker.js\"", csprojContent);
        Assert.Contains("PublishedContent=\"wwwroot\\service-worker.published.js\"", csprojContent);
        Assert.Contains("<ServiceWorkerAssetsManifest>service-worker-assets.js</ServiceWorkerAssetsManifest>", csprojContent);
    }

    [Fact]
    public void ProductionPrecacheInclusionExclusion_ConfiguredForBootAssetsOnly()
    {
        string wwwroot = GetWwwRootDirectory();
        string prodSwPath = Path.Combine(wwwroot, "service-worker.published.js");
        string prodSwContent = File.ReadAllText(prodSwPath);

        Assert.Contains("offlineAssetsInclude", prodSwContent);

        string[] requiredSubstrings = [@"/\.dll$/", @"/\.wasm$/", @"/\.html$/", @"/\.js$/", @"/\.json$/", @"/\.css$/", @"/\.woff2?$/", @"/\.ico$/", @"/manifest\.webmanifest$/", @"/android-chrome-\d+x\d+\.png$/"];
        foreach (string expected in requiredSubstrings)
        {
            Assert.Contains(expected, prodSwContent);
        }

        // Contract 3b: Must define offlineAssetsExclude excluding service worker script(s)
        Assert.Contains("offlineAssetsExclude", prodSwContent);
        Assert.Contains(@"service-worker\.js", prodSwContent);

        // Contract 3c: Heavy binary media (JPG, PNG, MP4) must NOT be included in precache array regex to avoid bloated offline storage
        Assert.DoesNotContain(@"/\.jpg$/", prodSwContent);
        Assert.DoesNotContain(@"/\.jpeg$/", prodSwContent);
        Assert.DoesNotContain(@"/\.mp4$/", prodSwContent);
    }

    [Fact]
    public void NavigationOfflineFallback_ConfiguredForClientRouting()
    {
        string wwwroot = GetWwwRootDirectory();
        string prodSwPath = Path.Combine(wwwroot, "service-worker.published.js");
        string prodSwContent = File.ReadAllText(prodSwPath);

        // Contract 4a: Must check navigation mode on fetch requests
        Assert.Contains("mode === 'navigate'", prodSwContent);

        // Contract 4b: Must fallback navigation requests to index.html
        Assert.Contains("index.html", prodSwContent);
        Assert.Contains("shouldServeIndexHtml", prodSwContent);
    }

    [Fact]
    public void ApiAndHubNetworkOnlyBehavior_ExcludesServerEndpoints()
    {
        string wwwroot = GetWwwRootDirectory();
        string prodSwPath = Path.Combine(wwwroot, "service-worker.published.js");
        string prodSwContent = File.ReadAllText(prodSwPath);

        // Contract 5a: Navigation fallback must explicitly exclude REST API endpoints (/api/)
        Assert.Contains("!event.request.url.includes('/api/')", prodSwContent);

        // Contract 5b: Navigation fallback must explicitly exclude SignalR hubs (/hubs/)
        Assert.Contains("!event.request.url.includes('/hubs/')", prodSwContent);
    }

    [Fact]
    public void VersionedOldCacheCleanup_PurgesStaleCachesOnActivation()
    {
        string wwwroot = GetWwwRootDirectory();
        string prodSwPath = Path.Combine(wwwroot, "service-worker.published.js");
        string prodSwContent = File.ReadAllText(prodSwPath);

        // Contract 6a: Uses versioned cache prefix and assetsManifest version
        Assert.Contains("cacheNamePrefix", prodSwContent);
        Assert.Contains("self.assetsManifest.version", prodSwContent);

        // Contract 6b: On activate, queries cache keys and deletes stale versions matching cacheNamePrefix
        Assert.Contains("caches.keys()", prodSwContent);
        Assert.Contains("caches.delete", prodSwContent);
        Assert.Contains("key.startsWith(cacheNamePrefix)", prodSwContent);
    }

    [Fact]
    public void RegistrationAndHostingHeaders_AllowPredictableWorkerUpdates()
    {
        string wwwroot = GetWwwRootDirectory();
        string indexHtmlContent = File.ReadAllText(Path.Combine(wwwroot, "index.html"));
        string apiProgramContent = File.ReadAllText(GetApiProgramPath());

        Assert.Contains("updateViaCache: 'none'", indexHtmlContent);
        Assert.Contains("registration.update()", indexHtmlContent);
        Assert.Contains("service-worker.js", apiProgramContent);
        Assert.Contains("service-worker-assets.js", apiProgramContent);
        Assert.Contains("manifest.webmanifest", apiProgramContent);
        Assert.Contains("Response.Headers.CacheControl = \"no-cache\"", apiProgramContent);
    }

    private static string GetWwwRootDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "code", "FinanceManager", "wwwroot");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            var altCandidate = Path.Combine(dir.FullName, "FinanceManager", "wwwroot");
            if (Directory.Exists(altCandidate))
            {
                return altCandidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate FinanceManager wwwroot directory relative to " + AppContext.BaseDirectory);
    }

    private static string GetWebUiCsprojPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "code", "FinanceManager", "FinanceManager.WebUi.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var altCandidate = Path.Combine(dir.FullName, "FinanceManager", "FinanceManager.WebUi.csproj");
            if (File.Exists(altCandidate))
            {
                return altCandidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate FinanceManager.WebUi.csproj relative to " + AppContext.BaseDirectory);
    }

    private static string GetApiProgramPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "code", "FinanceManager.Api", "Program.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var altCandidate = Path.Combine(dir.FullName, "FinanceManager.Api", "Program.cs");
            if (File.Exists(altCandidate))
            {
                return altCandidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate FinanceManager.Api/Program.cs relative to " + AppContext.BaseDirectory);
    }
}
// Screenshots FinanceManager pages for agents and humans in one command: signs in once through the
// develop-only auto test login, then navigates inside the running SPA (no WASM reboot per page).
//
// Usage (from the repository root, with the API already running — see .claude/skills/ui-testing):
//   dotnet run scripts/Screenshot.cs -- Transactions AccountDetails/3
//   dotnet run scripts/Screenshot.cs -- Transactions --viewports mobile,desktop --theme both --out /tmp/shots
// Targets are in-app routes ("" or "/" for the dashboard).
// Options: --base-url <url>   app address (default: $FM_BASE_URL or http://localhost:5113)
//          --viewports <list> mobile, desktop or WIDTHxHEIGHT, comma separated (default: mobile,desktop)
//          --theme <t>        light, dark or both (default: light)
//          --out <dir>        output directory (default: ./screenshots)
//          --login <name>     guest or testuser (default: guest)
//          --full-page        capture the whole scrollable page, not just the viewport
//          --install          download the Chromium build this Playwright version needs, then exit
#:package Microsoft.Playwright@1.64.1
// Playwright serializes its protocol with reflection, which file-based apps disable by default (they target AOT).
#:property PublishAot=false

using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

var baseUrl = Environment.GetEnvironmentVariable("FM_BASE_URL") ?? "http://localhost:5113";
var viewportNames = "mobile,desktop";
var theme = "light";
var outDirectory = "screenshots";
var login = "guest";
var fullPage = false;
List<string> targets = [];

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--base-url" when i + 1 < args.Length: baseUrl = args[++i]; break;
        case "--viewports" when i + 1 < args.Length: viewportNames = args[++i]; break;
        case "--theme" when i + 1 < args.Length: theme = args[++i]; break;
        case "--out" when i + 1 < args.Length: outDirectory = args[++i]; break;
        case "--login" when i + 1 < args.Length: login = args[++i]; break;
        case "--full-page": fullPage = true; break;
        case "--install": return Microsoft.Playwright.Program.Main(["install", "chromium"]);
        case "--help" or "-h": return Usage(null);
        default:
            if (args[i].StartsWith("--")) return Usage($"Unknown or incomplete option '{args[i]}'.");
            targets.Add(args[i]);
            break;
    }
}

if (targets.Count == 0) return Usage("Give at least one route, e.g. Transactions.");
baseUrl = baseUrl.TrimEnd('/');

List<(string Name, int Width, int Height)> viewports = [];
foreach (var name in viewportNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
{
    if (name == "mobile") viewports.Add((name, 430, 920));
    else if (name == "desktop") viewports.Add((name, 1280, 1000));
    else if (Regex.Match(name, @"^(\d+)x(\d+)$") is { Success: true } size)
        viewports.Add((name, int.Parse(size.Groups[1].Value), int.Parse(size.Groups[2].Value)));
    else return Usage($"Unknown viewport '{name}'. Use mobile, desktop or WIDTHxHEIGHT.");
}

string[] themes = theme switch
{
    "light" => ["light"],
    "dark" => ["dark"],
    "both" => ["light", "dark"],
    _ => [],
};
if (themes.Length == 0) return Usage($"Unknown theme '{theme}'. Use light, dark or both.");

using (var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
{
    try { await probe.GetAsync(baseUrl); }
    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
    {
        Console.Error.WriteLine($"error: nothing answers at {baseUrl}. Build and start the API first (.claude/skills/ui-testing/SKILL.md, steps 1-2) or pass --base-url.");
        return 2;
    }
}

Directory.CreateDirectory(outDirectory);
using var playwright = await Playwright.CreateAsync();
IBrowser browser;
try
{
    browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Args = ["--no-sandbox"] });
}
catch (PlaywrightException exception) when (exception.Message.Contains("Executable doesn't exist"))
{
    Console.Error.WriteLine("error: Playwright's Chromium is not installed. Run: dotnet run scripts/Screenshot.cs -- --install");
    return 3;
}

var failures = 0;
var stopwatch = Stopwatch.StartNew();
foreach (var colorScheme in themes)
{
    foreach (var viewport in viewports)
    {
        await using var context = await browser.NewContextAsync(new()
        {
            ViewportSize = new() { Width = viewport.Width, Height = viewport.Height },
            ColorScheme = colorScheme == "dark" ? ColorScheme.Dark : ColorScheme.Light,
            ReducedMotion = ReducedMotion.Reduce,
            IgnoreHTTPSErrors = true,
        });
        var page = await context.NewPageAsync();
        var network = new NetworkTracker(page);

        if (!await SignIn(page, network))
        {
            failures++;
            continue;
        }

        foreach (var target in targets)
        {
            var route = target.Trim('/');
            await page.EvaluateAsync("route => window.Blazor.navigateTo('/' + route)", route);
            await page.WaitForURLAsync(url => new Uri(url).AbsolutePath.Trim('/').Equals(route.Split('?')[0], StringComparison.OrdinalIgnoreCase), new() { Timeout = 30000 });
            await network.WaitForIdle();

            var path = Path.GetFullPath(Path.Combine(outDirectory, $"{Slug(route)}-{viewport.Name}-{colorScheme}.png"));
            await page.ScreenshotAsync(new() { Path = path, FullPage = fullPage });
            var warning = await page.IsVisibleAsync("#blazor-error-ui") ? "  (warning: Blazor error bar is visible)" : "";
            Console.WriteLine(path + warning);
        }
    }
}

await browser.CloseAsync();
Console.Error.WriteLine($"done in {stopwatch.Elapsed.TotalSeconds:0.0}s");
return failures == 0 ? 0 : 1;

async Task<bool> SignIn(IPage page, NetworkTracker network)
{
    await page.GotoAsync($"{baseUrl}/DevelopLogin/{login}", new() { Timeout = 120000 });
    // The login page redirects to the dashboard once the WASM app has signed in; it shows an error otherwise.
    var signedIn = page.WaitForURLAsync(url => !url.Contains("/DevelopLogin/", StringComparison.OrdinalIgnoreCase), new() { Timeout = 120000 });
    var failed = page.Locator("[data-testid=develop-login-error]").WaitForAsync(new() { Timeout = 120000 });
    if (await Task.WhenAny(signedIn, failed) == failed && failed.IsCompletedSuccessfully)
    {
        Console.Error.WriteLine($"error: {await page.Locator("[data-testid=develop-login-error]").InnerTextAsync()}");
        return false;
    }

    await signedIn;
    await network.WaitForIdle();
    return true;
}

static string Slug(string route) =>
    route.Length == 0 ? "dashboard" : Regex.Replace(route, "[^A-Za-z0-9]+", "-").Trim('-');

static int Usage(string? error)
{
    if (error is not null) Console.Error.WriteLine($"error: {error}");
    Console.Error.WriteLine("usage: dotnet run scripts/Screenshot.cs -- <route>... [--viewports mobile,desktop] [--theme light|dark|both] [--out dir] [--base-url url] [--full-page] [--install]");
    return error is null ? 0 : 1;
}

// Blazor pages keep loading data after navigation, so "ready" means no API request has been in flight
// for a short quiet period.
sealed class NetworkTracker
{
    private int _inFlight;
    private DateTime _lastActivity = DateTime.UtcNow;

    public NetworkTracker(IPage page)
    {
        page.Request += (_, _) => Touch(+1);
        page.RequestFinished += (_, _) => Touch(-1);
        page.RequestFailed += (_, _) => Touch(-1);
    }

    public async Task WaitForIdle(int quietMilliseconds = 750, int timeoutMilliseconds = 30000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (DateTime.UtcNow < deadline)
        {
            if (Volatile.Read(ref _inFlight) <= 0 && DateTime.UtcNow - _lastActivity > TimeSpan.FromMilliseconds(quietMilliseconds))
                return;
            await Task.Delay(100);
        }
    }

    private void Touch(int delta)
    {
        Interlocked.Add(ref _inFlight, delta);
        _lastActivity = DateTime.UtcNow;
    }
}

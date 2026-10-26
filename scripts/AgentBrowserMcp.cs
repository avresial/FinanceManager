// Local stdio MCP server that lets testing agents reach FinanceManager features and screenshot them through
// tools instead of shell commands. It wraps scripts/Screenshot.cs, so both stay in step.
//
// Build once (later starts reuse the cached build and answer within the client's start-up timeout):
//   dotnet build scripts/AgentBrowserMcp.cs
// Then register it in the MCP client, e.g. OpenCode's opencode.json at the repository root:
//   "mcp": { "financemanager-browser": { "type": "local", "command": ["dotnet", "run", "scripts/AgentBrowserMcp.cs"] } }
// The app must already be running (.claude/skills/ui-testing/SKILL.md); pass its address as baseUrl or set FM_BASE_URL.
#:package ModelContextProtocol@2.2.0
#:package Microsoft.Extensions.Hosting@10.0.0
// The MCP SDK discovers tools with reflection, which file-based apps disable by default (they target AOT).
#:property PublishAot=false

using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);
// stdout carries the MCP protocol, so every log line goes to stderr.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<BrowserTools>();
await builder.Build().RunAsync();

[McpServerToolType]
public sealed class BrowserTools
{
    private static readonly string _screenshotScript = Path.Combine(
        AppContext.GetData("EntryPointFileDirectoryPath") as string ?? Path.Combine(Directory.GetCurrentDirectory(), "scripts"),
        "Screenshot.cs");

    [McpServerTool(Name = "list_features", ReadOnly = true, Idempotent = true)]
    [Description("List the FinanceManager features you can screenshot: feature id, route and what it shows. Use a feature id as the target of the screenshot tool.")]
    public static Task<string> ListFeatures(CancellationToken cancellationToken) =>
        RunScreenshotScript(["--list"], cancellationToken);

    [McpServerTool(Name = "screenshot", ReadOnly = true)]
    [Description("Sign in to the running app as the demo guest, open one or more features and save PNG screenshots. Returns the file paths, one per feature, viewport and theme.")]
    public static Task<string> Screenshot(
        [Description("Feature ids from list_features (e.g. transactions, account-add-entry) or in-app routes, separated by spaces.")] string targets,
        [Description("Where to save the PNG files.")] string outDir,
        [Description("App address, e.g. http://localhost:5113. Defaults to FM_BASE_URL.")] string? baseUrl = null,
        [Description("mobile, desktop, or mobile,desktop.")] string viewports = "mobile,desktop",
        [Description("light, dark or both.")] string theme = "light",
        [Description("Optional data-testid (or CSS selector) to click on each page before the screenshot.")] string? clickTestId = null,
        CancellationToken cancellationToken = default)
    {
        List<string> arguments = [.. targets.Split(' ', StringSplitOptions.RemoveEmptyEntries), "--out", outDir, "--viewports", viewports, "--theme", theme];
        if (!string.IsNullOrWhiteSpace(baseUrl)) arguments.AddRange(["--base-url", baseUrl]);
        if (!string.IsNullOrWhiteSpace(clickTestId)) arguments.AddRange(["--click", clickTestId]);
        return RunScreenshotScript(arguments, cancellationToken);
    }

    private static async Task<string> RunScreenshotScript(IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(_screenshotScript)!,
        };
        foreach (var argument in (string[])["run", _screenshotScript, "--", .. arguments])
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errors = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var text = (await output).Trim();
        var problems = (await errors).Trim();
        return process.ExitCode == 0 ? $"{text}\n{problems}".Trim() : $"Failed (exit code {process.ExitCode}).\n{text}\n{problems}".Trim();
    }
}

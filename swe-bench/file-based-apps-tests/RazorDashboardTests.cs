using Xunit;

namespace Runfile.SweBench.Tests;

public sealed class RazorDashboardTests
{
    [Fact]
    public async Task HomePageStillRenders()
    {
        await using var app = await FileAppServer.StartAsync(Path.Combine("razorapp", "razorapp.cs"));

        var html = await app.Client.GetStringAsync("/");

        Assert.Contains("Hello, world!", html);
    }

    [Fact]
    public async Task DiagnosticsPageRendersRuntimeData()
    {
        await using var app = await FileAppServer.StartAsync(Path.Combine("razorapp", "razorapp.cs"));

        var html = await app.Client.GetStringAsync("/diagnostics");

        Assert.Contains("data-testid=\"diagnostics-page\"", html);
        Assert.Contains("data-testid=\"diagnostics-environment\"", html);
        Assert.Contains("data-testid=\"diagnostics-process-id\"", html);
        Assert.Contains("data-testid=\"diagnostics-entry-file\"", html);
        Assert.Contains("data-testid=\"diagnostics-entry-directory\"", html);
        Assert.Contains("data-testid=\"diagnostics-local-time\"", html);
        Assert.Contains("data-testid=\"diagnostics-refresh\"", html);
        Assert.Contains("razorapp.cs", html);
    }
}

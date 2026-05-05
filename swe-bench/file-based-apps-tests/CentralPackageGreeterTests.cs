using Xunit;

namespace Runfile.SweBench.Tests;

public sealed class CentralPackageGreeterTests
{
    private static readonly string GreetApp = Path.Combine("flat", "greet.cs");

    [Fact]
    public void HumanizerVersionIsCentrallyManaged()
    {
        var packagesPath = RepoPaths.Combine("Directory.Packages.props");
        Assert.True(File.Exists(packagesPath), "Expected Directory.Packages.props at the scenario root.");

        var packages = File.ReadAllText(packagesPath);
        Assert.Contains("PackageVersion", packages);
        Assert.Contains("Humanizer", packages);

        var source = File.ReadAllText(RepoPaths.Combine("flat", "greet.cs"));
        Assert.Contains("#:package Humanizer", source);
        Assert.DoesNotContain("#:package Humanizer@", source);
    }

    [Fact]
    public async Task GreetAppUsesHumanizerFeaturesWithCentralPackageVersion()
    {
        var result = await Dotnet.RunAsync("run", "--file", GreetApp, "--", "--count", "4", "grace", "hopper");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Hello, Grace Hopper!", result.Stdout);
        Assert.Contains("This is your 4th greeting.", result.Stdout);
    }
}

using Xunit;

namespace Runfile.SweBench.Tests;

public sealed class GreeterHumanizerTests
{
    private static readonly string GreetApp = Path.Combine("flat", "greet.cs");

    [Fact]
    public void GreetAppReferencesHumanizerWithPackageDirective()
    {
        var source = File.ReadAllText(RepoPaths.Combine("flat", "greet.cs"));

        Assert.Contains("#:package", source);
        Assert.Contains("Humanizer", source);
    }

    [Fact]
    public async Task LowercaseMultiWordNameIsTitleCased()
    {
        var result = await Dotnet.RunAsync("run", "--file", GreetApp, "--", "ada", "lovelace");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Hello, Ada Lovelace!", result.Stdout);
    }

    [Fact]
    public async Task CountOptionUsesOrdinalFormatting()
    {
        var result = await Dotnet.RunAsync("run", "--file", GreetApp, "--", "--count", "3", "ada", "lovelace");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Hello, Ada Lovelace!", result.Stdout);
        Assert.Contains("This is your 3rd greeting.", result.Stdout);
    }
}

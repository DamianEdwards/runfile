using Xunit;

namespace Runfile.SweBench.Tests;

public sealed class ProjectRefGreetingTests
{
    private static readonly string HelloApp = Path.Combine("classlibproject", "samples", "hello.cs");

    [Fact]
    public async Task SpanishGreetingUsesClassLibraryFormatting()
    {
        var result = await Dotnet.RunAsync("run", "--file", HelloApp, "--", "--name", "Ada", "--language", "es");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Hola, Ada!", result.Stdout.Trim());
    }

    [Fact]
    public async Task FrenchGreetingCanBeShouted()
    {
        var result = await Dotnet.RunAsync("run", "--file", HelloApp, "--", "--name", "Ada", "--language", "fr", "--shout");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("BONJOUR, ADA!", result.Stdout.Trim());
    }

    [Fact]
    public async Task UnsupportedLanguageReturnsTwo()
    {
        var result = await Dotnet.RunAsync("run", "--file", HelloApp, "--", "--name", "Ada", "--language", "de");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unsupported language: de", result.Stderr);
    }
}

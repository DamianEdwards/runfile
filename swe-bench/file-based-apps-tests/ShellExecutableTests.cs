using System.Text;
using Xunit;

namespace Runfile.SweBench.Tests;

public sealed class ShellExecutableTests
{
    private static readonly string ShellApp = Path.Combine("flat", "hello-shell.cs");

    [Fact]
    public void ShellAppHasShebangLfLineEndingsAndNoBom()
    {
        var path = RepoPaths.Combine("flat", "hello-shell.cs");
        var bytes = File.ReadAllBytes(path);
        var text = Encoding.UTF8.GetString(bytes);

        Assert.StartsWith("#!/usr/bin/env dotnet\n", text);
        Assert.DoesNotContain("\r\n", text);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "File should not include a UTF-8 BOM.");
    }

    [Fact]
    public async Task ShellAppRunsWithDotnetRunFile()
    {
        var result = await Dotnet.RunAsync("run", "--file", ShellApp, "--", "Ada");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Hello, Ada!", result.Stdout.Trim());
    }
}

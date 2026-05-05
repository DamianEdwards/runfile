using Xunit;

namespace Runfile.SweBench.Tests;

public sealed class AspireAppHostTests
{
    [Fact]
    public void AppHostUsesAspireSdkAndExistingFileApps()
    {
        var sourcePath = RepoPaths.Combine("flat", "apphost.cs");
        Assert.True(File.Exists(sourcePath), "Expected flat/apphost.cs.");

        var source = Normalize(File.ReadAllText(sourcePath));
        Assert.Contains("#:sdkAspire.AppHost.Sdk", source);
        Assert.Contains("AddCSharpApp(\"webapi\",\"./webapi.cs\")", source);
        Assert.Contains("AddCSharpApp(\"razorapp\",\"../razorapp/razorapp.cs\")", source);
        Assert.Contains(".WithReference(webapi)", source);
        Assert.Contains(".WaitFor(webapi)", source);
    }

    private static string Normalize(string source)
        => source.Replace("\r\n", "\n").Replace(" ", string.Empty);
}

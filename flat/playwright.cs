#!/usr/bin/env dotnet
#:package Microsoft.NET.Test.Sdk@18.10.0
#:package TUnit.Playwright@1.67.0
#:property PublishAot=false

using TUnit.Playwright;
using System.Diagnostics;
using System.Text.RegularExpressions;

public partial class Tests : PageTest
{
    [Test]
    public async Task HomepageHasPlaywrightInTitleAndGetStartedLinkLinkingtoTheIntroPage()
    {
        await Page.GotoAsync("https://playwright.dev");

        // Expect a title "to contain" a substring.
        await Expect(Page).ToHaveTitleAsync(PlaywrightRegex());

        // create a locator
        var getStarted = Page.Locator("text=Get Started");

        // Expect an attribute "to be strictly equal" to the value.
        await Expect(getStarted).ToHaveAttributeAsync("href", "/docs/intro");

        // Click the get started link.
        await getStarted.ClickAsync();

        // Expects the URL to contain intro.
        await Expect(Page).ToHaveURLAsync(IntroRegex());
    }

    [GeneratedRegex("Playwright")]
    public static partial Regex PlaywrightRegex();

    [GeneratedRegex(".*intro")]
    public static partial Regex IntroRegex();
}

public partial class GlobalHooks
{
    [Before(TestSession)]
    public static void InstallPlaywright()
    {
        Console.WriteLine("Installing Playwright browsers...");

        if (Debugger.IsAttached)
        {
            Environment.SetEnvironmentVariable("PWDEBUG", "1");
        }

        // Install Playwright browsers and dependencies
        // This handles cross-platform installation automatically
        var exitCode = Microsoft.Playwright.Program.Main(["install", "--with-deps"]);
        
        if (exitCode != 0)
        {
            Console.WriteLine($"Warning: Playwright install exited with code {exitCode}");
        }
    }
}

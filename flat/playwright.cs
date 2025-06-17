#!/usr/bin/dotnet run
#:package Microsoft.NET.Test.Sdk@17.*
#:package TUnit.Playwright@0.*

using TUnit.Playwright;
using System.Diagnostics;
using System.Text.RegularExpressions;

public class Tests : PageTest
{
    [Before(TestSession)]
    public static void InstallPlaywright()
    {
        //install playwright
        if (Debugger.IsAttached)
        {
            Environment.SetEnvironmentVariable("PWDEBUG", "1");
        }

        Microsoft.Playwright.Program.Main(["install-deps"]);
        Microsoft.Playwright.Program.Main(["install"]);
    }

    [Test]
    public async Task HomepageHasPlaywrightInTitleAndGetStartedLinkLinkingtoTheIntroPage()
    {
        await Page.GotoAsync("https://playwright.dev");

        // Expect a title "to contain" a substring.
        await Expect(Page).ToHaveTitleAsync(new Regex("Playwright"));

        // create a locator
        var getStarted = Page.Locator("text=Get Started");

        // Expect an attribute "to be strictly equal" to the value.
        await Expect(getStarted).ToHaveAttributeAsync("href", "/docs/intro");

        // Click the get started link.
        await getStarted.ClickAsync();

        // Expects the URL to contain intro.
        await Expect(Page).ToHaveURLAsync(new Regex(".*intro"));
    }
}

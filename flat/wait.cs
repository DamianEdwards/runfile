#!/usr/bin/dotnet run

#:package Humanizer@2.14.1

using System.Text.RegularExpressions;
using Humanizer;

var delay = TimeSpan.FromSeconds(1);

if (args.Length > 0)
{
    var arg = string.Join(' ', args);
    if (int.TryParse(arg, out var ms))
    {
        delay = TimeSpan.FromMilliseconds(ms);
    }
    else if (TimeSpan.TryParse(arg, out var ts))
    {
        delay = ts;
    }
    else if (TryParseDuration(arg, out var ts2))
    {
        delay = ts2;
    }
    else
    {
        Console.Error.WriteLine($"Invalid argument: {arg}");
        return 1;
    }
}

if (delay < TimeSpan.Zero)
{
    Console.Error.WriteLine("Negative wait value is not allowed.");
    return 2;
}
else if (delay == TimeSpan.Zero)
{
    Console.WriteLine("Wait value is zero, exiting immediately.");
}
else
{
    Console.WriteLine($"Waiting for {delay.Humanize(5)}…");
    await Task.Delay(delay);
}

return 0;

static bool TryParseDuration(string input, out TimeSpan result)
{
    var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    result = TimeSpan.Zero;

    foreach (var token in tokens)
    {
        var match = RegexParsers.Duration().Match(token);

        if (!match.Success)
        {
            return false;
        }

        var value = int.Parse(match.Groups["value"].Value);
        var unit = match.Groups["unit"].Value.ToLowerInvariant();
        var now = DateTime.Now;

        result += unit switch
        {
            "y" or "year" => now.AddYears(value) - now,
            "mo" or "month" => now.AddMonths(value) - now,
            "w" or "week" => TimeSpan.FromDays(value * 7),
            "d" or "day" => TimeSpan.FromDays(value),
            "h" or "hr" or "hour" => TimeSpan.FromHours(value),
            "m" or "min" or "minute" => TimeSpan.FromMinutes(value),
            "s" or "sec" or "second" => TimeSpan.FromSeconds(value),
            "ms" or "millisecond" => TimeSpan.FromMilliseconds(value),
            "microsecond" => TimeSpan.FromMicroseconds(value),
            _ => throw new InvalidOperationException($"Unknown time unit: {unit}")
        };
    }

    return true;
}

internal static partial class RegexParsers
{
    [GeneratedRegex(@"^(?<value>\d+)(?!\.\d)\s*(?<unit>ms|y|year|mo|month|w|wk|week|d|day|h|hr|hour|m|min|minute|s|sec|second|millisecond|microsecond)s?$", RegexOptions.IgnoreCase, "en-US")]
    public static partial Regex Duration();
}
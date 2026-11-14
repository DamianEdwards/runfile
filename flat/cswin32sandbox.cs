#!/usr/bin/env dotnet
#:property TargetFramework=net11.0-windows
#:package Microsoft.Windows.CsWin32@0.3.371
#:package Humanizer@3.0.10

using Windows.Win32;
using Humanizer;

var ticks = PInvoke.GetTickCount64();
Console.WriteLine($"System has been running for {TimeSpan.FromMilliseconds((long)ticks).Humanize(precision: 4)}.");

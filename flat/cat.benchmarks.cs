#!/usr/bin/dotnet run

#package BenchmarkDotNet 0.14.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<Benchmarks>();

[MemoryDiagnoser]
public class Benchmarks
{
    private readonly FileInfo SmallFile = new("D:\\src\\GitHub\\DamianEdwards\\RazorSlices\\samples\\RazorSlices.Samples.WebApp\\Slices\\Lorem\\LoremInjectableProperties.cshtml");
    private readonly FileInfo LargeFile = new("D:\\src\\GitHub\\DamianEdwards\\RazorSlices\\samples\\RazorSlices.Samples.WebApp\\Slices\\Lorem\\LoremStatic.cshtml");

    [Benchmark]
    public long PrintFileSmall() => FileHelpers.PrintFile(SmallFile);

    [Benchmark]
    public long PrintFileLarge() => FileHelpers.PrintFile(LargeFile);
}
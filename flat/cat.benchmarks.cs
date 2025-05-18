#!/usr/bin/dotnet run

#:package BenchmarkDotNet@0.14.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<Benchmarks>();

[MemoryDiagnoser]
public class Benchmarks
{
    private readonly FileInfo SmallFile = new("hello.cs");
    private readonly FileInfo MediumFile = new("The Tell-Tale Heart.txt");
    private readonly FileInfo LargeFile = new("Pride and Prejudice.txt");
    private readonly FileInfo ExtraLargeFile = new("War and Peace.txt");

    [Benchmark]
    public long PrintFileSmall() => FileHelpers.PrintFile(SmallFile);

    [Benchmark]
    public long PrintFileMedium() => FileHelpers.PrintFile(MediumFile);

    [Benchmark]
    public long PrintFileLarge() => FileHelpers.PrintFile(LargeFile);

    [Benchmark]
    public long PrintFileExtraLarge() => FileHelpers.PrintFile(ExtraLargeFile);
}
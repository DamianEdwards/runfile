#!/usr/bin/env dotnet
#:package System.CommandLine@2.0.0-beta5.*
#:property LangVersion=preview

using System.CommandLine;
using System.IO;

var fileArg = new Argument<FileInfo>(name: "file")
{
    Description = "The file to read and display on the console.",
    Arity = ArgumentArity.ExactlyOne
};
fileArg.AcceptExistingOnly();

var rootCommand = new RootCommand("C# implementation of the Unix 'cat' command")
{
    fileArg
};

rootCommand.SetAction((parseResult, ct) =>
{
    var result = 0;
    var fileInfo = parseResult.GetValue(fileArg);

    if (fileInfo?.Exists != true)
    {
        var color = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("File not found or no file specified.");
        Console.ForegroundColor = color;
        result = 1;
    }
    else
    {
        var linesRead = FileHelpers.PrintFile(fileInfo, ct);
    }

    return Task.FromResult(result);
});

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();

public static class FileHelpers
{
    public static long PrintFile(FileInfo file, CancellationToken cancellationToken)
    {
        Span<char> buffer = stackalloc char[256];

        using var readStream = File.OpenRead(file.FullName);
        using var reader = new StreamReader(readStream);
        long linesRead = 0;

        while (!reader.EndOfStream)
        {
            var charsRead = reader.ReadBlock(buffer);
            char? lastChar = null;

            for (var i = 0; i < charsRead; i++)
            {
                var c = buffer[i];
                Console.Write(c);

                if (c == '\n' && (Environment.NewLine == "\n" || lastChar == '\r'))
                {
                    linesRead++;
                }

                lastChar = c;
            }
        }

        return linesRead;
    }
}

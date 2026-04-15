#:package System.CommandLine@2.0.*
#:include ./cat.helpers.cs

using System.CommandLine;

namespace CatCli;

internal static class Commands
{
    public static Command DefineRootCommand()
    {
        var fileArg = new Argument<FileInfo>(name: "file")
        {
            Description = "The file to read and display on the console.",
            Arity = ArgumentArity.ExactlyOne
        };
        fileArg.AcceptExistingOnly();
        
        var showLineCountOption = new Option<bool>("--show-line-count")
        {
            Description = "Show the total number of lines read after displaying the file content.",
            Arity = ArgumentArity.ZeroOrOne
        };

        var rootCommand = new RootCommand("C# implementation of the Unix 'cat' command")
        {
            fileArg,
            showLineCountOption
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

                if (parseResult.GetValue(showLineCountOption))
                {
                    Console.WriteLine();
                    Console.WriteLine($"\nTotal lines read: {linesRead}");
                }
            }

            return Task.FromResult(result);
        });
        return rootCommand;
    }
}
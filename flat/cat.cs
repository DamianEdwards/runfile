#!/usr/bin/dotnet run

#package System.CommandLine 2.0.0-*

using System.CommandLine;

var fileOption = new Option<FileInfo?>(name: "--file", description: "The file to read and display on the console.");

var rootCommand = new RootCommand("Sample app for System.CommandLine");
rootCommand.AddOption(fileOption);
rootCommand.SetHandler(file => FileHelpers.PrintFile(file), fileOption);

return await rootCommand.InvokeAsync(args);

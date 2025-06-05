#:property LangVersion preview

Console.WriteLine($"Script path: {Path.ScriptPath()}");
Console.WriteLine($"Script directory: {Path.ScriptDirectory()}");

public static class PathExtensions
{
    extension(Path)
    {
        public static string ScriptPath() => ScriptPathImpl();

        public static string ScriptDirectory() => Path.GetDirectoryName(ScriptPathImpl()) ?? "";

        private static string ScriptPathImpl([System.Runtime.CompilerServices.CallerFilePath] string filePath = "") => filePath;
    }
}

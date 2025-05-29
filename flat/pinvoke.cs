#:property AllowUnsafeBlocks True

using System.Reflection;
using System.Runtime.InteropServices;

if (args.Contains("--debug"))
{
    NativeMethods.Debug = true;
    Console.WriteLine("Debug mode enabled.");
}

NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, NativeMethods.DllImportResolver);

var greeting = NativeMethods.Greetings("run-file");

if (OperatingSystem.IsWindows() && !args.Contains("--console"))
{
    NativeMethods.MessageBoxW(IntPtr.Zero, greeting, "Attention!", 0);
}
else
{
    Console.WriteLine(greeting);
}

internal partial class NativeMethods
{
    public static bool Debug { get; set; } = false;

    public static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {

        // Try to load the native library from the current directory.
        // BUG: If the file is run from a different directory, it will not find the library.
        //      See https://github.com/dotnet/sdk/issues/49184 for proposed workaround.
        var libraryPath = Path.Join(Environment.GetEnvironmentVariable("DOTNET_EXECUTING_FILE_DIRECTORY") ?? Environment.CurrentDirectory, GetLibraryName(libraryName));
        if (Debug)
        {
            Console.WriteLine($"Attempting to load library: {libraryPath}");
        }
        return NativeLibrary.TryLoad(libraryPath, out var handle)
            ? handle
            : IntPtr.Zero; // Fallback to the default import resolver.
    }

    private static string GetLibraryName(string libraryName) => OperatingSystem.IsWindows() ? libraryName : PrefixFileName("lib", libraryName);

    private static string PrefixFileName(string prefix, string path)
    {
        var dir = Path.GetDirectoryName(path);
        var fileName = $"{prefix}{Path.GetFileName(path)}";
        var ext = OperatingSystem.IsWindows()
            ? ".dll"
            : OperatingSystem.IsLinux()
                ? ".so"
                : OperatingSystem.IsMacOS()
                    ? ".dylib"
                    : throw new PlatformNotSupportedException("Unsupported platform for native library loading.");
        var result = Path.Join(dir ?? string.Empty, $"{fileName}{ext}");

        if (Debug)
        {
            Console.WriteLine($"Current dir: {Environment.CurrentDirectory}");
            Console.WriteLine($"Library dir: {dir}");
            Console.WriteLine($"Library file name: {fileName}");
            Console.WriteLine($"Prefixed result: {result}");
        }

        return result;
    }

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    public static partial int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);

    [LibraryImport("lib/greetings", StringMarshalling = StringMarshalling.Utf8)]
    public static partial string Greetings(string name);
}

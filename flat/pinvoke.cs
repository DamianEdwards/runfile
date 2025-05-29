#:property AllowUnsafeBlocks True

using System.Reflection;
using System.Runtime.InteropServices;

NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, NativeMethods.DllImportResolver);

var greeting = NativeMethods.Greetings("run-file");

if (OperatingSystem.IsWindows() && args is not [.., "--console"])
{
    NativeMethods.MessageBoxW(IntPtr.Zero, greeting, "Attention!", 0);
}
else
{
    Console.WriteLine(greeting);
}

internal partial class NativeMethods
{
    public static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath) =>
    
        // Try to load the native library from the current directory.
        // BUG: If the file is run from a different directory, it will not find the library.
        //      See https://github.com/dotnet/sdk/issues/49184 for proposed workaround.
        NativeLibrary.TryLoad(Path.Join(Environment.GetEnvironmentVariable("DOTNET_EXECUTING_FILE_DIRECTORY") ?? Environment.CurrentDirectory, libraryName), out var handle)
            ? handle
            : IntPtr.Zero; // Fallback to the default import resolver.

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    public static partial int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);

    [LibraryImport("lib/greetings", StringMarshalling = StringMarshalling.Utf8)]
    public static partial string Greetings(string name);
}

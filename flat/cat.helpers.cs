namespace System.IO;

public static class FileHelpers
{
    public static long PrintFile(FileInfo? file)
    {
        if (file?.Exists != true)
        {
            Console.WriteLine("File not found or no file specified.");
            return 0;
        }

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

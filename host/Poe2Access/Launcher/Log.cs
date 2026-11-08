namespace Poe2Access;

using System.Text;

/// <summary>Timestamped lines to Poe2Access.log: through the open <see cref="HostLog"/>, else appended by a short-lived command.</summary>
internal static class Log
{
    private static string? filePath;

    public static void Configure(string path) => filePath = path;

    public static void Write(string message)
    {
        if (HostLog.Line(message))
        {
            return;
        }

        try
        {
            if (filePath is null)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            using var file = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            file.Write(Encoding.UTF8.GetBytes($"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}"));
        }
        catch (IOException)
        {
            // Logging must never break the command.
        }
    }
}

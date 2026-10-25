namespace Poe2AccessLauncher;

/// <summary>Appends timestamped lines to Poe2Access.log next to the executable, or in the package's "app" folder.</summary>
internal static class Log
{
    private static readonly string FilePath = Path.Combine(
        Directory.Exists(HostController.PackagedHostDirectory) ? HostController.PackagedHostDirectory : AppContext.BaseDirectory, "Poe2Access.log");

    public static void Write(string message)
    {
        try
        {
            File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Logging must never break the launch.
        }
    }
}

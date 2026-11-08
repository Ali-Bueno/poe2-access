namespace Poe2Access;

using System.Runtime.InteropServices;

/// <summary>The message boxes of the start-up steps (an error, a question, a note), above a running game.</summary>
internal static partial class Dialogs
{
    public const uint Error = 0x10;
    public const uint Information = 0x40;
    private const int AttachParentProcess = -1;

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int Show(IntPtr window, string text, string caption, uint type);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int processId);

    public static void Fail(string message)
    {
        Log.Write($"ERROR {message}");
        Show(IntPtr.Zero, message, "Poe2Access", Error);
    }

    /// <summary>Prints to the console that started this program, else (started by double-click) to a message box.</summary>
    public static void ShowUsage(string usage)
    {
        if (AttachConsole(AttachParentProcess) || Console.IsOutputRedirected)
        {
            Console.WriteLine(usage);
        }
        else
        {
            Show(IntPtr.Zero, usage, "Poe2Access", Information);
        }
    }

    /// <summary>Attaches the starting console, so the extract command's output shows in it.</summary>
    public static void AttachParentConsole() => AttachConsole(AttachParentProcess);
}

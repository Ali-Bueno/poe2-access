namespace Poe2Access;

using System.Diagnostics;
using System.Runtime.InteropServices;
using Poe2Accessibility.Services;

/// <summary>
///     The message boxes of the start-up steps (an error, a question, a note), above a running game. Under Wine or Proton
///     they are asked on the Linux desktop instead, where Orca reads them (<see cref="HostDialog"/>).
/// </summary>
internal static partial class Dialogs
{
    public const uint Error = 0x10;
    public const uint Information = 0x40;
    private const int AttachParentProcess = -1;

    private static readonly TimeSpan DialogStart = TimeSpan.FromSeconds(15); // for sh to open zenity or kdialog
    private static readonly TimeSpan DialogPatience = TimeSpan.FromHours(1); // for the player to answer
    private static readonly TimeSpan DialogPoll = TimeSpan.FromMilliseconds(200);

    private static readonly Lazy<bool> UnderWine = new(() => PrismLibrary.WineVersion() is not null);

    /// <summary>Shows the message and returns MessageBoxW's answer.</summary>
    public static int Show(IntPtr window, string text, string caption, uint type) =>
        (UnderWine.Value ? OnLinuxDesktop(text, type) : null) ?? MessageBox(window, text, caption, type);

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBox(IntPtr window, string text, string caption, uint type);

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

    /// <summary>
    ///     Asks through zenity or kdialog on the Linux side (Wine's start.exe /unix runs a Unix program) and returns the
    ///     answer, or null when that could not be done (neither is installed, no Z: drive): the caller shows the message box.
    ///     The message and the answer travel through files, since an exit code does not come back through start.exe.
    /// </summary>
    private static int? OnLinuxDesktop(string text, uint type)
    {
        // A name nobody else on the machine can tell beforehand: the folder holds a script that sh then runs, and /tmp is
        // everyone's (a folder made ahead under a guessable name would let another user swap the script).
        var folder = Path.Join(@"Z:\tmp", $"poe2access-dialog-{Guid.NewGuid():N}");
        try
        {
            if (Directory.Exists(folder))
            {
                return null; // not ours: never write a script where someone else already is
            }

            Directory.CreateDirectory(folder);
            var unix = "/tmp/" + Path.GetFileName(folder);
            File.WriteAllText(Path.Join(folder, "dialog.sh"), HostDialog.Script.Replace("\r", string.Empty, StringComparison.Ordinal));
            File.WriteAllText(Path.Join(folder, "text"), text);
            var info = new ProcessStartInfo(Path.Join(Environment.SystemDirectory, "start.exe")) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var arg in new[] { "/wait", "/unix", "/bin/sh", unix + "/dialog.sh", unix + "/text", HostDialog.Kind(type), unix + "/answer", unix + "/open" })
            {
                info.ArgumentList.Add(arg);
            }

            using (var start = Process.Start(info))
            {
                start?.WaitForExit();
            }

            // start.exe returns at once for a Unix program: the dialog has opened once "open" exists, and has been
            // answered once "answer" does (written whole by a move).
            var answerFile = Path.Join(folder, "answer");
            var openFile = Path.Join(folder, "open");
            var began = Stopwatch.StartNew();
            while (!File.Exists(answerFile) && (File.Exists(openFile) ? began.Elapsed < DialogPatience : began.Elapsed < DialogStart))
            {
                Thread.Sleep(DialogPoll);
            }

            var answer = HostDialog.Answer(type, File.Exists(answerFile) ? File.ReadAllText(answerFile) : null);
            if (answer is null)
            {
                Log.Write("No zenity or kdialog answered: the message box is shown instead (Orca cannot read it)");
            }

            return answer;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Log.Write($"Could not ask on the Linux desktop: {error.Message}");
            return null;
        }
        finally
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}

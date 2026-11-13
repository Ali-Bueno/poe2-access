namespace Poe2Access;

/// <summary>
///     Under Wine or Proton a Windows message box is a bare X11 window that Orca cannot read, so a blind player never hears
///     the update question or an error. There the start-up steps ask the Linux desktop instead: zenity (GTK) or kdialog
///     (KDE), which Orca reads, run through Wine's start.exe /unix. This class holds the parts that do not touch Wine.
/// </summary>
internal static class HostDialog
{
    /// <summary>MB_YESNO, the only button set the start-up steps ask with besides OK.</summary>
    public const uint YesNo = 0x4;

    /// <summary>MB_ICONERROR (MB_ICONHAND).</summary>
    public const uint ErrorIcon = 0x10;

    /// <summary>The exit codes zenity and kdialog give a dialog that ran: Yes or OK, No or a closed window.</summary>
    private const int ExitYes = 0;
    private const int ExitNo = 1;

    /// <summary>MessageBoxW's answers.</summary>
    public const int AnswerOk = 1;
    public const int AnswerYes = 6;
    public const int AnswerNo = 7;

    /// <summary>
    ///     The script sh runs on the Linux side: $1 the message file, $2 the kind, $3 the file that gets the exit code, $4
    ///     the file it creates as soon as a dialog opens (Wine's start.exe /wait does not wait for a Unix program, so the
    ///     caller polls). With neither zenity nor kdialog installed it answers "none", and the caller shows the message box.
    /// </summary>
    public const string Script =
        "#!/bin/sh\n" +
        "# Written by Poe2Access: a message box Orca can read (zenity or kdialog) for the mod running under Wine or Proton.\n" +
        "text=$(cat \"$1\")\n" +
        "case $2 in\n" +
        "question) z=--question; k=--yesno ;;\n" +
        "error) z=--error; k=--error ;;\n" +
        "*) z=--info; k=--msgbox ;;\n" +
        "esac\n" +
        "if command -v zenity >/dev/null 2>&1; then\n" +
        "\t: >\"$4\"\n" +
        "\tzenity \"$z\" --no-markup --title=Poe2Access --text=\"$text\"\n" +
        "elif command -v kdialog >/dev/null 2>&1; then\n" +
        "\t: >\"$4\"\n" +
        "\tkdialog --title Poe2Access \"$k\" \"$text\"\n" +
        "else\n" +
        "\techo none >\"$3.part\"\n" +
        "\tmv -f \"$3.part\" \"$3\"\n" +
        "\texit 0\n" +
        "fi\n" +
        "echo $? >\"$3.part\"\n" +
        "mv -f \"$3.part\" \"$3\"\n";

    /// <summary>The kind of dialog the script shows for a MessageBoxW type.</summary>
    public static string Kind(uint type) =>
        (type & 0xF) == YesNo ? "question" : (type & 0xF0) == ErrorIcon ? "error" : "info";

    /// <summary>
    ///     MessageBoxW's answer for what the script wrote (the dialog's exit code), or null when it wrote nothing usable
    ///     ("none": no zenity nor kdialog) or a code of a tool that failed to run: zenity and kdialog exit 0 for Yes or OK, 1
    ///     for No or a closed window, and any other code (a missing display, a crash) is no answer, so the message box asks.
    /// </summary>
    public static int? Answer(uint type, string? written)
    {
        if (!int.TryParse(written?.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var code) ||
            code is not (ExitYes or ExitNo))
        {
            return null;
        }

        return Kind(type) == "question" ? (code == 0 ? AnswerYes : AnswerNo) : AnswerOk;
    }
}

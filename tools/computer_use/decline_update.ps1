# Answers "No" on the play copy's update question so a game started through Steam starts for a test (AGENTS.md rule 28).
# It reads the box's text first and leaves any other dialog alone; "No" declines for that start only, and the owner is
# asked again at theirs. It never answers "Yes": that would replace the play copy.
#
#   cmd /c start "" "steam://rungameid/2694490"; pwsh -NoProfile -File tools/computer_use/decline_update.ps1
#
# Prints what it did: "declined", "another dialog, left alone", "game already starting" or "no update question appeared".
param([int]$WaitSeconds = 60)

# BM_CLICK presses a button as a click would, with no key press to switch the game to the keyboard.
$buttonClick = 0x00F5

Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices;
public static class UpdateDialog {
  public delegate bool Cb(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(Cb cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, Cb cb, IntPtr l);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  public static string Decline(uint want, uint click) {
    string outcome = "no dialog";
    EnumWindows((h, l) => {
      uint pid; GetWindowThreadProcessId(h, out pid);
      if (pid != want || !IsWindowVisible(h)) return true;
      IntPtr no = IntPtr.Zero; bool question = false;
      EnumChildWindows(h, (c, l2) => {
        var s = new StringBuilder(2048); GetWindowText(c, s, 2048); var t = s.ToString();
        if (t == "&No") no = c;
        // The question in the launcher's two languages (Launcher/Strings: "Hay una versión nueva…", "A new version of Poe2Access…").
        if (t.Contains("Hay una versi") || t.Contains("A new version of Poe2Access")) question = true;
        return true; }, IntPtr.Zero);
      if (question && no != IntPtr.Zero) { PostMessage(no, click, IntPtr.Zero, IntPtr.Zero); outcome = "declined"; }
      else if (no != IntPtr.Zero) outcome = "another dialog, left alone";
      return true; }, IntPtr.Zero);
    return outcome; } }
'@

$deadline = (Get-Date).AddSeconds($WaitSeconds)
do {
    $launcher = Get-CimInstance Win32_Process -Filter "Name = 'Poe2Access.exe'" | Where-Object { $_.ExecutablePath -like '*\dist\Poe2Access\Poe2Access.exe' }
    foreach ($process in $launcher) {
        $outcome = [UpdateDialog]::Decline([uint32]$process.ProcessId, [uint32]$buttonClick)
        if ($outcome -ne 'no dialog') { "launcher $($process.ProcessId): $outcome"; exit 0 }
    }

    if (Get-Process | Where-Object { $_.ProcessName -match 'PathOfExile' }) { 'game already starting: no question asked'; exit 0 }
    Start-Sleep -Milliseconds 700
} while ((Get-Date) -lt $deadline)
'no update question appeared'

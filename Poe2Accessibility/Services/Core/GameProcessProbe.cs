namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using GameOffsets;

    /// <summary>
    ///     Diagnoses "game is running but GameHelper did not attach" for a user who cannot see the overlay.
    ///     The host attaches to a process that <c>GameProcessDetails.IsGame</c> takes for the game (its name, and the 2 in
    ///     its window's title or its folder), so the same rule is reported here.
    /// </summary>
    public sealed class GameProcessProbe
    {
        /// <summary>Process enumeration is not free; while unattached, look this often.</summary>
        private const int ProbeIntervalMs = 5000;

        private readonly PluginLog log;
        private readonly Stopwatch sinceLastProbe = Stopwatch.StartNew();
        private string lastReport = string.Empty;
        private string pendingReport = string.Empty;

        public GameProcessProbe(PluginLog log)
        {
            this.log = log;
        }

        /// <summary>Raised when the diagnosis changes (a matching process appeared, its title changed, it went away).</summary>
        public event Action<string>? ReportChanged;

        /// <summary>Human-readable diagnosis, empty when no game-like process exists.</summary>
        public string Describe()
        {
            var lines = new List<string>();
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (!GameProcessDetails.ProcessNames.Contains(process.ProcessName))
                    {
                        continue;
                    }

                    var title = process.MainWindowTitle;
                    lines.Add(GameProcessDetails.IsGame(process.ProcessName, title, ImagePath(process))
                        ? Announcements.GameProcessMatches(process.ProcessName)
                        : Announcements.GameProcessTitleMismatch(process.ProcessName, title));
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Process exited or access denied (elevated game vs. non-elevated host).
                    lines.Add(Announcements.GameProcessInaccessible(process.ProcessName));
                }
                finally
                {
                    process.Dispose();
                }
            }

            return string.Join(" ", lines);
        }

        /// <summary>Where the process runs from; null when Windows will not say, which leaves the title to tell.</summary>
        private static string? ImagePath(Process process)
        {
            try
            {
                return process.MainModule?.FileName;
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                return null;
            }
        }

        /// <summary>Call every frame while unattached; throttled internally.</summary>
        public void Tick()
        {
            if (this.sinceLastProbe.ElapsedMilliseconds < ProbeIntervalMs)
            {
                return;
            }

            this.sinceLastProbe.Restart();
            var report = this.Describe();
            if (report == this.lastReport)
            {
                return;
            }

            // A game starting or quitting has no window for a moment ("no main window yet" was said as the player
            // quit, 2026-10-24): only a diagnosis found on two probes in a row is said.
            var seenBefore = report == this.pendingReport;
            this.pendingReport = report;
            if (!seenBefore && report.Length > 0)
            {
                return;
            }

            this.lastReport = report;
            if (!string.IsNullOrEmpty(report))
            {
                this.log.Warn($"Game process seen but the host has not attached: {report}");
                this.ReportChanged?.Invoke(report);
            }
        }

        public void Reset() => this.lastReport = this.pendingReport = string.Empty;
    }
}

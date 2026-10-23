namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using GameOffsets;

    /// <summary>
    ///     Diagnoses "game is running but GameHelper did not attach" for a user who cannot see the overlay.
    ///     GameHelper attaches only when the process name is in GameProcessDetails.ProcessName AND the main
    ///     window title equals the expected one, so we report exactly those two facts.
    /// </summary>
    public sealed class GameProcessProbe
    {
        /// <summary>Process enumeration is not free; while unattached, look this often.</summary>
        private const int ProbeIntervalMs = 5000;

        private readonly PluginLog log;
        private readonly Stopwatch sinceLastProbe = Stopwatch.StartNew();
        private string lastReport = string.Empty;

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
                    if (!GameProcessDetails.ProcessName.TryGetValue(process.ProcessName, out var expectedTitle))
                    {
                        continue;
                    }

                    var title = process.MainWindowTitle;
                    lines.Add(string.Equals(title.ToLowerInvariant(), expectedTitle, StringComparison.Ordinal)
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

            this.lastReport = report;
            if (!string.IsNullOrEmpty(report))
            {
                this.log.Warn($"Game process seen but GameHelper not attached: {report}");
                this.ReportChanged?.Invoke(report);
            }
        }

        public void Reset() => this.lastReport = string.Empty;
    }
}

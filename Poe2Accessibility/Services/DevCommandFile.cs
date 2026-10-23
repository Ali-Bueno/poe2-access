namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;

    /// <summary>
    ///     Research-mode command file (debug/command.txt): lets an automated tester request dumps without a
    ///     key press, which would switch the game out of controller input.
    /// </summary>
    public sealed class DevCommandFile
    {
        private const int PollIntervalMs = 500;

        private readonly string path;
        private readonly PluginLog log;
        private readonly Stopwatch sinceCheck = Stopwatch.StartNew();

        public DevCommandFile(string debugDirectory, PluginLog log)
        {
            this.path = Path.Join(debugDirectory, "command.txt");
            this.log = log;
        }

        /// <summary>Returns the commands written since the last poll (lower-case, one per line) and deletes the file.</summary>
        public IReadOnlyList<string> Poll()
        {
            if (this.sinceCheck.ElapsedMilliseconds < PollIntervalMs || !File.Exists(this.path))
            {
                return Array.Empty<string>();
            }

            this.sinceCheck.Restart();
            try
            {
                var commands = File.ReadAllLines(this.path)
                    .Select(line => line.Trim().ToLowerInvariant())
                    .Where(line => line.Length > 0)
                    .ToList();
                File.Delete(this.path);
                this.log.Info($"Dev commands: {string.Join(", ", commands)}");
                return commands;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        }
    }
}

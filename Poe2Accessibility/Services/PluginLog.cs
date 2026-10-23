namespace Poe2Accessibility.Services
{
    using System;
    using System.IO;

    /// <summary>
    ///     Console (GameHelper2 convention) + file log under the plugin folder. Never call per frame.
    /// </summary>
    public sealed class PluginLog
    {
        private const string Prefix = "[Poe2Accessibility]";
        private readonly string filePath;
        private readonly object gate = new();

        public PluginLog(string logDirectory, bool verbose)
        {
            this.Verbose = verbose;
            Directory.CreateDirectory(logDirectory);
            this.filePath = Path.Join(logDirectory, "accessibility.log");
        }

        public bool Verbose { get; set; }

        public void Info(string message) => this.Write("INFO", message);

        public void Warn(string message) => this.Write("WARN", message);

        public void Error(string message, Exception? ex = null) =>
            this.Write("ERROR", ex == null ? message : $"{message}: {ex}");

        public void Debug(string message)
        {
            if (this.Verbose)
            {
                this.Write("DEBUG", message);
            }
        }

        private void Write(string level, string message)
        {
            // One entry per line: game texts carry their own line breaks.
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message.Replace("\r", string.Empty).Replace("\n", " | ")}";
            Console.WriteLine($"{Prefix} {line}");
            lock (this.gate)
            {
                try
                {
                    File.AppendAllText(this.filePath, line + Environment.NewLine);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Logging must never break the overlay.
                }
            }
        }
    }
}

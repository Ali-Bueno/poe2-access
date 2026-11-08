namespace Poe2Accessibility.Services
{
    using System;
    using System.IO;

    /// <summary>File log under the plugin folder, kept open and flushed per line. Never call per frame.</summary>
    public sealed class PluginLog
    {
        /// <summary>A log past this size at start is kept as .old and a new one begun.</summary>
        private const long RotateBytes = 8 * 1024 * 1024;

        private readonly object gate = new();
        private readonly StreamWriter? writer;

        public PluginLog(string logDirectory, bool verbose)
        {
            this.Verbose = verbose;
            Directory.CreateDirectory(logDirectory);
            var path = Path.Join(logDirectory, "accessibility.log");
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length > RotateBytes)
                {
                    File.Move(path, path + ".old", overwrite: true);
                }

                var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                this.writer = new StreamWriter(stream) { AutoFlush = true };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                this.writer = null; // logging must never stop the mod
            }
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
            lock (this.gate)
            {
                try
                {
                    this.writer?.WriteLine(line);
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException)
                {
                }
            }
        }
    }
}

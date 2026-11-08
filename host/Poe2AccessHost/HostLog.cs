namespace Poe2AccessHost
{
    using System;
    using System.IO;
    using System.Text;

    /// <summary>The host's own log; the reader's console messages go to it too, up to a size cap.</summary>
    internal sealed class HostLog : TextWriter
    {
        private const long MaxBytes = 4 * 1024 * 1024;

        private static HostLog? current;
        private readonly StreamWriter file;
        private long written;

        private HostLog(StreamWriter file) => this.file = file;

        public override Encoding Encoding => Encoding.UTF8;

        /// <summary>Starts a fresh log (the last one kept as .old) and takes over the console.</summary>
        public static HostLog Open(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Move(path, path + ".old", overwrite: true);
                }
            }
            catch (IOException)
            {
            }

            var log = new HostLog(new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true });
            current = log;
            Console.SetOut(log);
            Console.SetError(log);
            return log;
        }

        public static void Line(string line) => current?.WriteLine(line);

        public override void Write(char value) => this.Write(value.ToString());

        public override void Write(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            lock (this.file)
            {
                if (this.written > MaxBytes)
                {
                    return;
                }

                this.written += value.Length;
                this.file.Write(this.written > MaxBytes ? "[log full]" + Environment.NewLine : value);
            }
        }

        public override void WriteLine(string? value) => this.Write($"{DateTime.Now:HH:mm:ss.fff} {value}{Environment.NewLine}");

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                current = null;
                lock (this.file)
                {
                    this.file.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}

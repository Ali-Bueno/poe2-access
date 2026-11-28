namespace Poe2Accessibility.Services
{
    using System;
    using System.IO;

    /// <summary>
    ///     A true or false option of the game's own config file (Documents\My Games\Path of Exile 2\poe2_production_Config.ini),
    ///     which the game writes when an option is saved. The file is read again only when its write time changes, looked at
    ///     no more than once every <see cref="CheckMs"/>.
    /// </summary>
    public sealed class GameConfigOption
    {
        /// <summary>How often the file's write time is looked at.</summary>
        private const int CheckMs = 2000;

        private readonly Func<string> path;
        private readonly string key;
        private readonly Action<string>? log;
        private long checkedAt = long.MinValue / 2;
        private DateTime readStamp = DateTime.MinValue;
        private bool? value;
        private bool? said;
        private bool reported;

        public GameConfigOption(Func<string> path, string key, Action<string>? log = null)
        {
            this.path = path;
            this.key = key;
            this.log = log;
        }

        /// <summary>The option's value; null when the file or the key cannot be read.</summary>
        public bool? Value
        {
            get
            {
                var now = Environment.TickCount64;
                if (now - this.checkedAt < CheckMs)
                {
                    return this.value;
                }

                this.checkedAt = now;
                try
                {
                    var file = this.path();
                    var stamp = File.Exists(file) ? File.GetLastWriteTimeUtc(file) : DateTime.MinValue;
                    if (stamp != this.readStamp)
                    {
                        this.readStamp = stamp;
                        this.value = stamp == DateTime.MinValue ? null : Parse(File.ReadAllText(file), this.key);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    this.value = null; // the game may be writing it: looked at again later
                    this.readStamp = DateTime.MinValue;
                }

                this.Report();
                return this.value;
            }
        }

        /// <summary>The value of the first "key=true" or "key=false" line of an ini text, whatever its section; null when absent or neither.</summary>
        public static bool? Parse(string iniText, string key)
        {
            foreach (var raw in iniText.Split('\n'))
            {
                var line = raw.Trim();
                var equals = line.IndexOf('=');
                if (equals <= 0 || !line.AsSpan(0, equals).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var text = line[(equals + 1)..].Trim();
                return text.Equals("true", StringComparison.OrdinalIgnoreCase) ? true
                    : text.Equals("false", StringComparison.OrdinalIgnoreCase) ? false
                    : null;
            }

            return null;
        }

        /// <summary>Logs the value the first time and whenever it changes.</summary>
        private void Report()
        {
            if (this.reported && this.said == this.value)
            {
                return;
            }

            this.log?.Invoke(this.value is { } known ? $"Game option {this.key}={(known ? "true" : "false")}" : $"Game option {this.key} not readable");
            (this.said, this.reported) = (this.value, true);
        }
    }
}

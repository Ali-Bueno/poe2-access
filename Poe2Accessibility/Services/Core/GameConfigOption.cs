namespace Poe2Accessibility.Services
{
    using System;
    using System.IO;

    /// <summary>
    ///     An option of the game's own config file (Documents\My Games\Path of Exile 2\poe2_production_Config.ini), which
    ///     the game writes when an option is saved. The file is read again only when its write time changes, looked at
    ///     no more than once every <see cref="CheckMs"/>.
    /// </summary>
    public sealed class GameConfigOption
    {
        /// <summary>How often the file's write time is looked at.</summary>
        private const int CheckMs = 2000;

        /// <summary>
        ///     How long a value read before stands while the file cannot be read (the game writing it): past that it is not
        ///     known any more, and what depends on it falls back to hiding nothing.
        /// </summary>
        public const int StaleMs = 10000;

        private readonly Func<string> path;
        private readonly string section;
        private readonly string key;
        private readonly Action<string>? log;
        private readonly Func<long> clock;
        private long checkedAt = long.MinValue / 2;
        private long unreadSince = -1;
        private DateTime readStamp = DateTime.MinValue;
        private string? text;
        private string? said;
        private bool reported;

        /// <param name="section">The section the game keeps the key in ("UI"): the same key elsewhere is another option.</param>
        /// <param name="clock">Milliseconds that only go forward; the system's when not given (a test gives its own).</param>
        public GameConfigOption(Func<string> path, string section, string key, Action<string>? log = null, Func<long>? clock = null)
        {
            this.path = path;
            this.section = section;
            this.key = key;
            this.log = log;
            this.clock = clock ?? (() => Environment.TickCount64);
        }

        /// <summary>The option as true or false; null when the file or the key cannot be read, or it says neither.</summary>
        public bool? Value => AsBool(this.Text);

        /// <summary>The option's text as the file has it; null when the file or the key cannot be read.</summary>
        public string? Text
        {
            get
            {
                var now = this.clock();
                if (now - this.checkedAt < CheckMs)
                {
                    return this.text;
                }

                this.checkedAt = now;
                try
                {
                    var file = this.path();
                    var stamp = File.Exists(file) ? File.GetLastWriteTimeUtc(file) : DateTime.MinValue;
                    if (stamp != this.readStamp)
                    {
                        this.readStamp = stamp;
                        this.text = stamp == DateTime.MinValue ? null : ParseText(File.ReadAllText(file), this.section, this.key);
                    }

                    this.unreadSince = -1;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // The game may be writing it: the last value stands for a moment (dropping it at once would bring every
                    // hidden item back, drop sounds and all), and the file is read again next time. Not for good: a file
                    // that stays unread says nothing of what the player has chosen since.
                    this.readStamp = DateTime.MinValue;
                    this.unreadSince = this.unreadSince < 0 ? now : this.unreadSince;
                    if (now - this.unreadSince >= StaleMs)
                    {
                        this.text = null;
                    }
                }

                this.Report();
                return this.text;
            }
        }

        /// <summary>"true" or "false", whatever the case; null for anything else.</summary>
        public static bool? AsBool(string? text) =>
            string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) ? true
            : string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) ? false
            : null;

        /// <summary>The value of "key=true" or "key=false" in a section of an ini text; null when absent or neither.</summary>
        public static bool? Parse(string iniText, string section, string key) => AsBool(ParseText(iniText, section, key));

        /// <summary>The text after "key=" in a section of an ini text, trimmed; null when the section or the key is absent.</summary>
        public static string? ParseText(string iniText, string section, string key)
        {
            var inSection = false;
            foreach (var raw in iniText.Split('\n'))
            {
                var line = raw.Trim().TrimStart('﻿');
                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    inSection = line.AsSpan(1, line.Length - 2).Trim().Equals(section, StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                var equals = line.IndexOf('=');
                if (!inSection || equals <= 0 || !line.AsSpan(0, equals).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return line[(equals + 1)..].Trim();
            }

            return null;
        }

        /// <summary>Logs the value the first time and whenever it changes.</summary>
        private void Report()
        {
            if (this.reported && this.said == this.text)
            {
                return;
            }

            this.log?.Invoke(this.text is { } known ? $"Game option {this.key}={known}" : $"Game option {this.key} not readable");
            (this.said, this.reported) = (this.text, true);
        }
    }
}

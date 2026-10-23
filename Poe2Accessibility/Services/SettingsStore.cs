namespace Poe2Accessibility.Services
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using Newtonsoft.Json;

    /// <summary>
    ///     JSON settings file that a blind user edits by hand while GameHelper runs. The host calls
    ///     SaveSettings periodically, so we only write when our in-memory copy actually changed, and we
    ///     reload when the file on disk is newer than what we last loaded or wrote. A file that fails to
    ///     parse never replaces the running settings (and is therefore never overwritten with defaults).
    /// </summary>
    public sealed class SettingsStore<T>
        where T : class, new()
    {
        /// <summary>How often the file's timestamp is checked (a stat call, not a read).</summary>
        private const int ReloadCheckIntervalMs = 1000;

        private readonly string path;
        private readonly PluginLog log;
        private readonly Stopwatch sinceLastCheck = Stopwatch.StartNew();

        /// <summary>Canonical serialization of the settings currently in force; a save that matches it is skipped.</summary>
        private string lastCanonicalText = string.Empty;
        private DateTime lastWriteTimeUtc = DateTime.MinValue;

        public SettingsStore(string path, PluginLog log)
        {
            this.path = path;
            this.log = log;
        }

        public enum ReloadOutcome
        {
            Unchanged,
            Reloaded,
            Failed,
        }

        /// <summary>Loads the file, or returns <paramref name="fallback"/> when it is missing or unreadable.</summary>
        public T Load(T fallback)
        {
            if (!File.Exists(this.path))
            {
                return fallback;
            }

            try
            {
                var writeTime = File.GetLastWriteTimeUtc(this.path);
                var loaded = JsonConvert.DeserializeObject<T>(File.ReadAllText(this.path));
                if (loaded == null)
                {
                    throw new JsonException("file is empty or not a JSON object");
                }

                this.lastWriteTimeUtc = writeTime;
                this.lastCanonicalText = Canonical(loaded);
                return loaded;
            }
            catch (Exception ex)
            {
                this.log.Error($"Settings unreadable, keeping current values ({this.path})", ex);
                return fallback;
            }
        }

        public void Save(T settings)
        {
            try
            {
                var text = Canonical(settings);
                if (text == this.lastCanonicalText)
                {
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(this.path) ?? string.Empty);
                File.WriteAllText(this.path, text);
                this.lastCanonicalText = text;
                this.lastWriteTimeUtc = File.GetLastWriteTimeUtc(this.path);
            }
            catch (Exception ex)
            {
                this.log.Error($"Settings could not be saved ({this.path})", ex);
            }
        }

        /// <summary>Reloads when the file changed on disk since the last load/save; a broken file yields Failed and keeps <paramref name="current"/>.</summary>
        public ReloadOutcome ReloadIfChanged(T current, out T settings)
        {
            settings = current;
            if (this.sinceLastCheck.ElapsedMilliseconds < ReloadCheckIntervalMs)
            {
                return ReloadOutcome.Unchanged;
            }

            this.sinceLastCheck.Restart();
            DateTime writeTime;
            try
            {
                if (!File.Exists(this.path))
                {
                    return ReloadOutcome.Unchanged;
                }

                writeTime = File.GetLastWriteTimeUtc(this.path);
            }
            catch (IOException)
            {
                return ReloadOutcome.Unchanged;
            }

            if (writeTime <= this.lastWriteTimeUtc)
            {
                return ReloadOutcome.Unchanged;
            }

            var before = this.lastWriteTimeUtc;
            settings = this.Load(current);
            if (this.lastWriteTimeUtc == before)
            {
                // Load failed: remember this version so a broken file is reported once, not every second.
                this.lastWriteTimeUtc = writeTime;
                return ReloadOutcome.Failed;
            }

            this.log.Info("Settings file changed on disk, reloaded");
            return ReloadOutcome.Reloaded;
        }

        private static string Canonical(T settings) => JsonConvert.SerializeObject(settings, Formatting.Indented);
    }
}

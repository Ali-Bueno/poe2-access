namespace Poe2Accessibility
{
    using System;
    using System.IO;

    /// <summary>The language of the mod's own spoken phrases; it follows the game's text language.</summary>
    public static class Localization
    {
        private const string ConfigRelativePath = "My Games\\Path of Exile 2\\poe2_production_Config.ini";

        public static string Language { get; private set; } = "en";

        public static bool IsSpanish => Language == "es";

        /// <summary>Picks the language from an "en"/"es" override, else the game's config; never throws.</summary>
        public static string Detect(string? overrideSetting, Action<string>? log = null)
        {
            try
            {
                var wanted = overrideSetting?.Trim().ToLowerInvariant();
                if (wanted is "en" or "es")
                {
                    Language = wanted;
                    log?.Invoke($"Language '{Language}' (setting)");
                    return Language;
                }

                var fromGame = ReadGameLanguage(ConfigPath());
                Language = fromGame == "es" ? "es" : "en";
                log?.Invoke($"Language '{Language}' (game language '{fromGame ?? "unknown"}')");
            }
            catch (Exception ex)
            {
                Language = "en";
                log?.Invoke($"Language detection failed, using 'en': {ex.Message}");
            }

            return Language;
        }

        /// <summary>The value of "language=" under [LANGUAGE] in the given ini text, lower case; null when absent.</summary>
        public static string? ParseGameLanguage(string iniText)
        {
            var inSection = false;
            foreach (var raw in iniText.Split('\n'))
            {
                var line = raw.Trim();
                if (line.StartsWith('['))
                {
                    inSection = line.Equals("[LANGUAGE]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                var eq = line.IndexOf('=');
                if (inSection && eq > 0 && line[..eq].Trim().Equals("language", StringComparison.OrdinalIgnoreCase))
                {
                    return line[(eq + 1)..].Trim().ToLowerInvariant();
                }
            }

            return null;
        }

        private static string? ReadGameLanguage(string path) => File.Exists(path) ? ParseGameLanguage(File.ReadAllText(path)) : null;

        private static string ConfigPath()
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(documents))
            {
                documents = Path.Join(Environment.GetEnvironmentVariable("USERPROFILE") ?? string.Empty, "Documents");
            }

            return Path.Join(documents, ConfigRelativePath);
        }
    }
}

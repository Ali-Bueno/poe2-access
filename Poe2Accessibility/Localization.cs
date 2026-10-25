namespace Poe2Accessibility
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    /// <summary>The language of the mod's own spoken phrases; it follows the game's text language.</summary>
    public static class Localization
    {
        private const string ConfigRelativePath = "My Games\\Path of Exile 2\\poe2_production_Config.ini";

        /// <summary>The game's text languages, as its config and the area and item catalogs name them.</summary>
        public static readonly string[] Languages = { "en", "es", "pt", "fr", "de", "ru", "th", "ja", "ko", "zh-TW" };

        private static Dictionary<string, string> phrases = new(StringComparer.Ordinal);

        public static string Language { get; private set; } = "en";

        public static bool IsSpanish => Language == "es";

        /// <summary>Where catalog/lang/&lt;language&gt;.json tables live (English and Spanish are in the code).</summary>
        public static string? PhraseDirectory { get; set; }

        /// <summary>Picks the language from a language-code override, else the game's config; never throws.</summary>
        public static string Detect(string? overrideSetting, Action<string>? log = null)
        {
            try
            {
                if (Normalize(overrideSetting) is { } wanted)
                {
                    Use(wanted, log);
                    log?.Invoke($"Language '{Language}' (setting)");
                    return Language;
                }

                var fromGame = ReadGameLanguage(ConfigPath());
                Use(Normalize(fromGame) ?? "en", log);
                log?.Invoke($"Language '{Language}' (game language '{fromGame ?? "unknown"}')");
            }
            catch (Exception ex)
            {
                Use("en", log);
                log?.Invoke($"Language detection failed, using 'en': {ex.Message}");
            }

            return Language;
        }

        /// <summary>The supported language a code names ("ES", "pt-BR", "zh_tw"), or null.</summary>
        public static string? Normalize(string? code)
        {
            var wanted = code?.Trim().Replace('_', '-');
            if (string.IsNullOrEmpty(wanted))
            {
                return null;
            }

            var exact = Languages.FirstOrDefault(language => language.Equals(wanted, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                return exact;
            }

            var dash = wanted.IndexOf('-');
            var stem = dash > 0 ? wanted[..dash] : wanted;
            return stem.Equals("zh", StringComparison.OrdinalIgnoreCase)
                ? "zh-TW"
                : Languages.FirstOrDefault(language => language.Equals(stem, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The current language's translation of an English phrase (a composite format for interpolated ones).</summary>
        public static string? Translate(string english) => phrases.TryGetValue(english, out var translated) ? translated : null;

        /// <summary>For tests: a language with the given phrase table.</summary>
        public static void Set(string language, Dictionary<string, string> table)
        {
            Language = language;
            phrases = table;
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

        private static void Use(string language, Action<string>? log)
        {
            Language = language;
            phrases = language is "en" or "es" ? new(StringComparer.Ordinal) : LoadPhrases(language, log);
        }

        private static Dictionary<string, string> LoadPhrases(string language, Action<string>? log)
        {
            var file = PhraseDirectory == null ? null : Path.Join(PhraseDirectory, $"{language}.json");
            if (file == null || !File.Exists(file))
            {
                log?.Invoke($"Phrases for '{language}' not found ({file ?? "no directory"}); English is spoken");
                return new(StringComparer.Ordinal);
            }

            try
            {
                var table = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file)) ?? new();
                log?.Invoke($"Phrases for '{language}': {table.Count}");
                return new(table, StringComparer.Ordinal);
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                log?.Invoke($"Phrases for '{language}' unreadable, English is spoken: {ex.Message}");
                return new(StringComparer.Ordinal);
            }
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

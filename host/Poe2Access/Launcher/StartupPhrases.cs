namespace Poe2Access;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Poe2Accessibility;

/// <summary>The few lines said while the game data is regenerated: Spanish and English here, the other languages from the plugin's catalog/lang tables.</summary>
internal static class StartupPhrases
{
    // T(english, spanish): tools/localization/phrases.py reads these like the plugin's Announcements, for the other languages' tables.
    public static readonly Phrase Starting = T("Updating the game data after the patch; this takes a few minutes", "Actualizando los datos del juego tras el parche; tarda unos minutos");
    public static readonly Phrase Progress = T("Game data: {0} of {1}", "Datos del juego: {0} de {1}");
    public static readonly Phrase Ready = T("Game data ready", "Datos del juego listos");
    public static readonly Phrase Partial = T("Game data ready; not updated: {0} of {1}", "Datos del juego listos; sin actualizar: {0} de {1}");
    public static readonly Phrase StepSilent = T("Still working on {0}, about {1} min", "Sigo trabajando en {0}, unos {1} min");
    public static readonly Phrase WaitingForGame = T("Still waiting for the game window", "Sigo esperando la ventana del juego");
    public static readonly Phrase PluginMissing = T("The accessibility plugin is missing or did not load, so the game will not be spoken", "El plugin de accesibilidad falta o no se cargó, así que el juego no se leerá");

    private const string SettingsLanguage = "Language";
    private const string Spanish = "es";
    private const string English = "en";

    /// <summary>A line in the two languages kept in code; its English text is the key of the other languages' tables.</summary>
    public readonly record struct Phrase(string English, string Spanish);

    /// <summary>
    ///     The language of the plugin: its setting (settings.txt JSON) when it names one, else the game's, else the system's;
    ///     a code of <see cref="Localization.Languages"/>.
    /// </summary>
    public static string ResolveLanguage(string? settingsText, string? gameLanguage, string systemLanguage) =>
        Localization.Normalize(SettingLanguage(settingsText)) ?? Localization.Normalize(gameLanguage) ?? Localization.Normalize(systemLanguage) ?? English;

    /// <summary>The phrase in <paramref name="language"/>: Spanish or English from code, else the table's entry for the English key, else English.</summary>
    public static string Text(Phrase phrase, string language, IReadOnlyDictionary<string, string>? table) =>
        language == Spanish ? phrase.Spanish
        : language != English && table != null && table.TryGetValue(phrase.English, out var other) ? other
        : phrase.English;

    public static string Format(Phrase phrase, string language, IReadOnlyDictionary<string, string>? table, params object[] arguments) =>
        string.Format(CultureInfo.InvariantCulture, Text(phrase, language, table), arguments);

    private static Phrase T(string english, string spanish) => new(english, spanish);

    /// <summary>The plugin's phrase table for a language (English key to text); null for Spanish, English or a missing file.</summary>
    public static IReadOnlyDictionary<string, string>? LoadTable(string file)
    {
        try
        {
            return File.Exists(file) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file)) : null;
        }
        catch (Exception error) when (error is IOException or JsonException)
        {
            return null;
        }
    }

    private static string? SettingLanguage(string? settingsText)
    {
        if (string.IsNullOrWhiteSpace(settingsText))
        {
            return null;
        }

        try
        {
            using var settings = JsonDocument.Parse(settingsText, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            return settings.RootElement.ValueKind == JsonValueKind.Object && settings.RootElement.TryGetProperty(SettingsLanguage, out var value) &&
                   value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

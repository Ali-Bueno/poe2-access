namespace Poe2Access;

using Poe2Accessibility;

/// <summary>The launcher's spoken lines through the plugin folder's prism.dll, in the plugin's language; PRISM opens at the first line.</summary>
internal sealed class LauncherVoice(string pluginFolder) : IDisposable
{
    private const string SettingsFile = "settings.txt";
    private const string ConfigFolder = "config";
    private const string PhraseFolder = "catalog/lang";

    private StartupSpeech? speech;
    private string language = string.Empty;
    private IReadOnlyDictionary<string, string>? table;
    private bool opened;

    /// <summary>Says the phrase (silent, logged, when PRISM is not there); <paramref name="arguments"/> fill its placeholders.</summary>
    public void Say(StartupPhrases.Phrase phrase, bool interrupt = false, params object[] arguments)
    {
        this.Open();
        this.speech?.Speak(arguments.Length == 0
            ? StartupPhrases.Text(phrase, this.language, this.table)
            : StartupPhrases.Format(phrase, this.language, this.table, arguments), interrupt);
    }

    public void Dispose() => this.Dispose(0);

    /// <param name="holdMilliseconds">How long the last line is left to be heard before PRISM goes.</param>
    public void Dispose(int holdMilliseconds) => this.speech?.Dispose(holdMilliseconds);

    private void Open()
    {
        if (this.opened)
        {
            return;
        }

        this.opened = true;
        var settings = Path.Join(pluginFolder, ConfigFolder, SettingsFile);
        this.language = StartupPhrases.ResolveLanguage(
            File.Exists(settings) ? File.ReadAllText(settings) : null, Localization.GameLanguage(), System.Globalization.CultureInfo.CurrentUICulture.Name);
        this.table = StartupPhrases.LoadTable(Path.Join(pluginFolder, PhraseFolder, this.language + ".json"));
        this.speech = StartupSpeech.TryCreate(pluginFolder);
        Log.Write($"Launcher speech in '{this.language}'");
    }
}

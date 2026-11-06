using System.Text;
using Index = LibBundle3.Index;

/// <summary>
///     The game's own link from a chest-like object to its sound: its Chests row names <c>.ao</c> files, and an
///     <c>.ao</c>'s <c>SoundEvents</c> say which FMOD event each animation plays. Read from the data, so an object a patch
///     adds has its sound with no word list to extend.
/// </summary>
internal static class ObjectSoundLinks
{
    private const string Table = "chests";

    // Chests: the Id at the row's start, the AOFiles string list at +0x15 (row 1 read by hand, 2026-11-06).
    private const int IdColumn = 0;
    private const int AoFilesColumn = 0x15;
    private const string AoExtension = ".ao";

    /// <summary>Every Chests row's opening sound, by its metadata path; a row with none is left out.</summary>
    /// <returns>Null when the table cannot be read.</returns>
    public static Dictionary<string, string>? Read(Index index)
    {
        if (DatTable.Open(index, Table) is not { } table)
        {
            return null;
        }

        var files = index.Files.Values.Where(file => file.Path != null && file.Path.EndsWith(AoExtension, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(file => file.Path!, StringComparer.OrdinalIgnoreCase);
        string? Text(string path) => files.TryGetValue(path, out var file) ? Encoding.Unicode.GetString(file.Read().ToArray()) : null;

        var links = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var row = 0; row < table.RowCount; row++)
        {
            var sound = table.StringList(row, AoFilesColumn).Select(path => AoSoundEvents.OpeningEventOf(path, Text)).FirstOrDefault(found => found != null);
            if (sound != null)
            {
                links[table.String(row, IdColumn)] = sound;
            }
        }

        return links;
    }
}

namespace Poe2Accessibility.Services
{
    using System;
    using System.Text.RegularExpressions;

    /// <summary>
    ///     What a world map's picture says of something within an area: what it is (the map legend's word for the picture, a
    ///     boss, a chest) and whether it is done, which the game draws by a second picture, grey and crossed out
    ///     ("WorldMapContentBoss" and "WorldMapContentBossDone"; docs/research/endgame-ui.md §7).
    /// </summary>
    public static class MapContentIcons
    {
        private const string DoneSuffix = "Done";

        /// <summary>The pictures of things within an area; a panel's frame in the same folder ("WorldMapInfoBg") is none.</summary>
        private static readonly Regex Picture = new(@"(?:^|/)(WorldMap(?:Content\w+|\w*Rune\w*|\w*Runstone\w*|Essence\w*))(?:$|\.)", RegexOptions.Compiled);

        /// <summary>
        ///     Pictures of the done state with a name of their own: the MapPins rows the game marks as done (+0xC5) whose picture
        ///     does not end in "Done" (0.5.5f: which altar half is set, which draught was drunk, which scale was chosen).
        /// </summary>
        private static readonly Regex OtherDone = new("(?:SnakePotion(?:Red|Green|Blue)|GoddessOfJustice(?:Left|Right)Active|FragmentAltar(?:Left|Right)Active)$", RegexOptions.Compiled);

        /// <summary>The picture's name without its done ending and whether it is the done one; null for any other picture.</summary>
        /// <param name="art">A texture key ("Art/…/WorldMap/WorldMapContentBossDone.dds|0,0…") or a MapPins row's icon ("Act4/WorldMapContent…").</param>
        public static (string Icon, bool Done)? Read(string art)
        {
            var found = Picture.Match(art);
            if (!found.Success)
            {
                return null;
            }

            var name = found.Groups[1].Value;
            return name.EndsWith(DoneSuffix, StringComparison.Ordinal) ? (name[..^DoneSuffix.Length], true) : (name, OtherDone.IsMatch(name));
        }

        /// <summary>A thing within an area, by its name and picture: "Azote de los cielos, completado", "Cangrejo muerto, Encuentro de monstruo raro, pendiente".</summary>
        /// <param name="kindOf">The map legend's word for a picture, empty for none.</param>
        public static string Describe(string name, string art, Func<string, string> kindOf)
        {
            if (Read(art) is not { } picture)
            {
                return name;
            }

            // The map's own legend shows each picture beside the word it stands for: no thing within an area.
            var kind = kindOf(picture.Icon);
            return kind.Length > 0 && kind == name ? name : name + Announcements.MapContentState(picture.Done, kind);
        }
    }
}

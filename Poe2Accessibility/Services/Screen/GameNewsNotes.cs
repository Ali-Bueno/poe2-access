namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    /// <summary>
    ///     What the mod will make of each key a game update added (<see cref="GameNews"/>), from the catalogs it already
    ///     has: a monster's dodge data (<see cref="DodgeCoverage"/>), a buff's rule or hazard, an area's name, a minimap
    ///     icon's kind, a screen's place in the UI catalog. Pure apart from <see cref="Load"/>.
    /// </summary>
    public sealed class GameNewsNotes
    {
        public const string MonsterTable = "monstervarieties";
        public const string BuffTable = "buffdefinitions";
        public const string AreaTable = "worldareas";
        public const string IconTable = "minimapicons";
        public const string AnimationTable = "animation";
        public const string ScreenTable = "screens";

        private const string HitTimesFile = "hit-times.txt";
        private const string AreaNamesFile = "area-names.json";
        private const string UiCatalogFile = "ui-catalog.json";
        private const string HazardsFile = "hazards.txt";
        private const string ControlsProperty = "controls";
        private const string ScreenProperty = "screen";

        private readonly DodgeCoverage dodge;
        private readonly ISet<string> namedAreas;
        private readonly ISet<string> screens;
        private readonly Func<string, bool> isHazard;

        public GameNewsNotes(DodgeCoverage dodge, ISet<string> namedAreas, ISet<string> screens, Func<string, bool> isHazard)
        {
            this.dodge = dodge;
            this.namedAreas = namedAreas;
            this.screens = screens;
            this.isHazard = isHazard;
        }

        /// <summary>From the plugin's catalog folder; a catalog missing or unreadable only leaves its keys unclassified.</summary>
        public static GameNewsNotes Load(string catalogDirectory)
        {
            var dodge = DodgeCoverage.Parse(AttackData.ReadLines(Path.Join(catalogDirectory, HitTimesFile)));
            var hazards = HazardGround.Load(Path.Join(catalogDirectory, HazardsFile));
            return new GameNewsNotes(dodge, AreaIds(Path.Join(catalogDirectory, AreaNamesFile)), Screens(Path.Join(catalogDirectory, UiCatalogFile)), hazards.IsHazard);
        }

        /// <summary>The note for one new key; null when nothing in the mod classifies that table's keys.</summary>
        public NewsNote? Of(string table, string key) => table switch
        {
            MonsterTable => this.Monster(key),
            BuffTable => Buff(key, this.isHazard),
            AreaTable => this.namedAreas.Contains(key) ? new NewsNote("named", true) : new NewsNote("no name in area-names", false),
            IconTable => new NewsNote(MinimapMarks.IsQuestIcon(key) ? "quest icon" : MinimapMarks.IsRitualAltar(key) ? "ritual altar" : "device, named by its icon", true),
            AnimationTable => new NewsNote($"the name reads as {DodgeThreats.Of(1, _ => key)}; the kind comes from each monster's skills", true),
            ScreenTable => this.screens.Contains(key) ? new NewsNote("in the UI catalog", true) : new NewsNote("not in the UI catalog", false),
            _ => null,
        };

        private NewsNote Monster(string key)
        {
            var boss = DodgeCoverage.IsBoss(key) ? "boss; " : string.Empty;
            return this.dodge.Of(key) is { } counts
                ? new NewsNote($"{boss}named by the game; dodge: {counts}", true)
                : new NewsNote($"{boss}named by the game; no attack data", false);
        }

        private static NewsNote Buff(string key, Func<string, bool> isHazard)
        {
            if (StatusAilments.FromBuffId(key) is { } ailment)
            {
                return new NewsNote($"player ailment {ailment}", true);
            }

            if (EnemyStates.FromBuffId(key) is { } state)
            {
                return new NewsNote($"enemy state {state}", true);
            }

            if (PlayerCharges.FromBuffId(key) is { } charge)
            {
                return new NewsNote($"charge {charge}", true);
            }

            if (BuffIds.IsHiddenMonster(key))
            {
                return new NewsNote("hidden monster", true);
            }

            return isHazard(key) ? new NewsNote("harmful ground", true) : new NewsNote("no rule names it; said by the game's name only", false);
        }

        /// <summary>The area ids named in any language ({ language: { id: name } }).</summary>
        private static HashSet<string> AreaIds(string path)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (Json(path) is { } document)
            {
                using (document)
                {
                    foreach (var language in document.RootElement.EnumerateObject())
                    {
                        foreach (var area in language.Value.EnumerateObject())
                        {
                            ids.Add(area.Name);
                        }
                    }
                }
            }

            return ids;
        }

        /// <summary>The .ui screens whose controls the UI catalog holds.</summary>
        private static HashSet<string> Screens(string path)
        {
            var screens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Json(path) is { } document)
            {
                using (document)
                {
                    if (document.RootElement.TryGetProperty(ControlsProperty, out var controls))
                    {
                        foreach (var control in controls.EnumerateArray())
                        {
                            if (control.TryGetProperty(ScreenProperty, out var screen) && screen.GetString() is { } name)
                            {
                                screens.Add(name);
                            }
                        }
                    }
                }
            }

            return screens;
        }

        private static JsonDocument? Json(string path)
        {
            try
            {
                return File.Exists(path) && JsonDocument.Parse(File.ReadAllText(path)) is { RootElement.ValueKind: JsonValueKind.Object } document ? document : null;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
            {
                return null;
            }
        }
    }
}

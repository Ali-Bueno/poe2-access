namespace Poe2Accessibility
{
    using System.Linq;

    /// <summary>Surroundings: scanner entries and categories, nearby things, other players, minions, rarity and quest text.</summary>
    public static partial class Announcements
    {
        /// <summary>What the quest map shows (its surface/underground toggle).</summary>
        public static string AboveGround => T("above ground", "encima del suelo");

        public static string BelowGround => T("below ground", "debajo del suelo");

        /// <summary>The left stick's eight directions, by <see cref="Services.StickDirection"/> octant.</summary>
        public static string[] StickDirections => new[]
        {
            T("right", "derecha"), T("up right", "arriba a la derecha"), T("up", "arriba"), T("up left", "arriba a la izquierda"),
            T("left", "izquierda"), T("down left", "abajo a la izquierda"), T("down", "abajo"), T("down right", "abajo a la derecha"),
        };

        public static string ScanCategoryName(Services.ScanCategory category) => category switch
        {
            Services.ScanCategory.Quest => T("Quest", "Misión"),
            Services.ScanCategory.Exits => T("Exits", "Salidas"),
            Services.ScanCategory.PointsOfInterest => T("Points of interest", "Puntos de interés"),
            Services.ScanCategory.Places => T("Places", "Lugares"),
            Services.ScanCategory.Unexplored => T("Unexplored", "Sin explorar"),
            Services.ScanCategory.Waypoints => T("Waypoints and checkpoints", "Puntos de ruta y de control"),
            Services.ScanCategory.Encounters => T("Encounters", "Encuentros"),
            Services.ScanCategory.Npcs => T("Characters", "Personajes"),
            Services.ScanCategory.Enemies => T("Enemies", "Enemigos"),
            Services.ScanCategory.Objects => T("Objects", "Elementos"),
            Services.ScanCategory.Chests => T("Chests and shrines", "Cofres y santuarios"),
            Services.ScanCategory.Items => T("Items", "Objetos"),
            Services.ScanCategory.Players => T("Players", "Jugadores"),
            _ => category.ToString(),
        };

        /// <summary>
        ///     D2R order: name, distance, direction ("Wounded Man, 9 steps, up left"); with a turn ahead, the steps
        ///     until it and the way it turns ("116 steps, down 3, then left"); "here" when on top of it; "no route"
        ///     when only the straight line is known.
        /// </summary>
        public static string ScanEntry(string name, int steps, string direction, int legSteps, string? then, bool routed, bool partial = false, bool barrier = false, bool longWay = false)
        {
            if (steps < 1)
            {
                return Here(name);
            }

            var leg = then == null ? direction : T($"{direction} {legSteps}, then {then}", $"{direction} {legSteps}, luego {then}");
            if (partial && barrier)
            {
                return T($"{name}, {Steps(steps)} away, a closed barrier cuts the way: {leg}",
                         $"{name}, a {Steps(steps)}, una barrera cerrada corta el camino: {leg}");
            }

            if (partial && longWay)
            {
                // The search ran out of budget: not a closed way, so not "stops short".
                return T($"{name}, {Steps(steps)} away, the way is long: {leg}", $"{name}, a {Steps(steps)}, la ruta es larga: {leg}");
            }

            if (partial)
            {
                // Steps are the straight line; the legs lead as close as the ground goes (a gate, a bridge further on).
                return T($"{name}, {Steps(steps)} away, the way stops short: {leg}",
                         $"{name}, a {Steps(steps)}, el camino se corta antes: {leg}");
            }

            var route = routed ? string.Empty : T(", no route", ", sin ruta");
            return T($"{name}, {Steps(steps)}, {leg}{route}", $"{name}, {Steps(steps)}, {leg}{route}");
        }

        /// <summary>Something within reach: "Una, here".</summary>
        public static string Here(string name) => T($"{name}, here", $"{name}, aquí");

        /// <summary>A count of scanner steps: "1 step", "12 steps".</summary>
        public static string Steps(int steps) => steps == 1 ? T("1 step", "1 paso") : T($"{steps} steps", $"{steps} pasos");

        /// <summary>Another player: character name, then class ("Yarky, Witch").</summary>
        public static string OtherPlayer(string name, string characterClass, int level = 0)
        {
            var spoken = name.Replace('_', ' ').Trim(); // "O_GROSSO_J" was read with its underscores (2026-10-24)
            var who = characterClass.Length == 0 || characterClass == name ? spoken : $"{spoken}, {characterClass}";
            return level > 0 ? T($"{who}, level {level}", $"{who}, nivel {level}") : who;
        }

        /// <summary>
        ///     A label for a control known only by its id (nothing on screen names it): the humanised id ("league"),
        ///     translated where the game's own word is known; other ids stay as they are.
        /// </summary>
        public static string Percent(int value) => T($"{value} percent", $"{value} por ciento");

        /// <summary>"3 Skeletal Archer, 80 percent" (one: no count).</summary>
        public static string MinionGroup(string name, int count, int lifePercent) => count > 1 ? $"{count} {name}, {Percent(lifePercent)}" : $"{name}, {Percent(lifePercent)}";

        public static string Minions(System.Collections.Generic.IEnumerable<string> groups) => T("Minions: ", "Esbirros: ") + string.Join("; ", groups);

        public static string EnemyWithRarity(string name, GameReader.RemoteEnums.Rarity rarity) => $"{name}, {RarityName(rarity)}";

        internal static string RarityName(GameReader.RemoteEnums.Rarity rarity) => rarity switch
        {
            GameReader.RemoteEnums.Rarity.Normal => T("normal", "normal"),
            GameReader.RemoteEnums.Rarity.Magic => T("magic", "mágico"),
            GameReader.RemoteEnums.Rarity.Rare => T("rare", "raro"),
            GameReader.RemoteEnums.Rarity.Unique => T("unique", "único"),
            _ => rarity.ToString().ToLowerInvariant(),
        };

        /// <summary>The quest tracker's shown page: titles and objectives.</summary>
        public static string QuestPage(string page) => T("Quest: ", "Misión: ") + page;

        /// <summary>
        ///     A quest reward by its icon's kind ("SupportGem"); Spanish as the game's item classes say it (ItemClasses:
        ///     "Gemas de asistencia", "Viales" for charms, "Tablilla"). A kind not known yet is said as its art name.
        /// </summary>
        public static string QuestReward(string kind) => kind.ToLowerInvariant() switch
        {
            "artificersorb" => Services.ItemNames.Of("CurrencyAddEquipmentSocket") ?? T("Artificer's Orb", "Orbe del artesano"),
            "atlaspassive" => T("Atlas passive point", "punto de pasiva del Atlas"),
            "book" => T("book", "libro"),
            "charms" => T("charms", "viales"),
            "consumablebuff" => T("lasting bonus", "bonificación duradera"),
            "craftingbench" => T("crafting bench", "mesa de fabricación"),
            "flasks" => T("flasks", "frascos"),
            "gold" => Gold,
            "precursortablet" => T("tablet", "tablilla"),
            "skillgem" => T("skill gem", "gema de habilidad"),
            "spiritdraught" => T("spirit", "espíritu"),
            "supportgem" => T("support gem", "gema de asistencia"),
            _ => kind,
        };

        /// <summary>What the player just walked up to: "Checkpoint", "Una, Stash".</summary>
        public static string Nearby(System.Collections.Generic.IEnumerable<string> names)
        {
            // Several of a kind are counted, as enemies are ("Tiesto, 3. Botella, 2"): "Tiesto, Tiesto, Tiesto" was heard in a room of pots.
            var groups = names.GroupBy(name => name).Select(group => (Name: group.Key, Count: group.Count())).ToList();
            return groups.All(group => group.Count == 1)
                ? string.Join(", ", groups.Select(group => group.Name))
                : string.Join(". ", groups.Select(group => group.Count > 1 ? $"{group.Name}, {group.Count}" : group.Name));
        }
    }
}

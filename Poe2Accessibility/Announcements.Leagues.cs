namespace Poe2Accessibility
{
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;

    /// <summary>Spoken strings of the league mechanics: their objects' names, progress, shrines, the fog, Expedition charges.</summary>
    public static partial class Announcements
    {
        /// <summary>The scanner's name of a league object the game shows none for; a marker by what it would dig up.</summary>
        public static string LeaguePieceName(LeaguePiece piece, ExpeditionFind? find) => piece switch
        {
            LeaguePiece.VerisiumRemnant => T("Verisium Remnant", "Remanente de verisium"),
            LeaguePiece.ExpeditionMarker => find switch
            {
                ExpeditionFind.Chest => T("Expedition chest", "Cofre de expedición"),
                ExpeditionFind.Elite => T("Expedition elite", "Élite de expedición"),
                ExpeditionFind.Monsters => T("Expedition monsters", "Monstruos de expedición"),
                ExpeditionFind.SpecialMonsters => T("Expedition special monsters", "Monstruos especiales de expedición"),
                ExpeditionFind.Boss => T("Expedition boss", "Jefe de expedición"),
                _ => T("Expedition marker", "Marcador de expedición"),
            },
            LeaguePiece.ExpeditionRelic => T("Expedition relic", "Reliquia de expedición"),
            LeaguePiece.ShardBoss => T("Delirium shard: boss", "Fragmento de delirio: jefe"),
            LeaguePiece.ShardMirroredBoss => T("Delirium shard: mirrored boss", "Fragmento de delirio: jefe reflejado"),
            LeaguePiece.ShardPortal => T("Delirium shard: portal", "Fragmento de delirio: portal"),
            LeaguePiece.ShardElites => T("Delirium shard: elites", "Fragmento de delirio: élites"),
            LeaguePiece.ShardPauseFog => T("Delirium shard: pauses the fog", "Fragmento de delirio: pausa la niebla"),
            LeaguePiece.ShardRareMods => T("Delirium shard: Delirium modifiers on rares", "Fragmento de delirio: modificadores de delirio en los raros"),
            LeaguePiece.ShardGrandMirror => T("Delirium shard: Grand Mirror", "Fragmento de delirio: Espejo grande"),
            LeaguePiece.GrandMirror => T("Grand Mirror", "Espejo grande"),
            LeaguePiece.AzmeriSpirit => T("Azmeri spirit", "Espíritu azmeri"),
            LeaguePiece.BreachSkill => T("Breach skill", "Habilidad de fisura"),
            _ => string.Empty,
        };

        /// <summary>A league object's news; <paramref name="name"/> is the game's name of a strongbox or an exile, <paramref name="wave"/> the Simulacrum's from 1.</summary>
        public static string League(LeagueLine line, string name, int wave) => line switch
        {
            LeagueLine.RemnantTome => T("Verisium Remnant: open the Runeshape Tome", "Remanente de verisium: abre el Tomo de runas"),
            LeagueLine.RemnantWaves => T("Verisium Remnant: waves of monsters", "Remanente de verisium: oleadas de monstruos"),
            LeagueLine.RemnantReward => T("Verisium Remnant: reward ready, shatter it", "Remanente de verisium: recompensa lista, rómpelo"),
            LeagueLine.RemnantSpoiled => T("Verisium Remnant spoiled", "Remanente de verisium estropeado"),
            LeagueLine.DetonatorReady => T("Detonator Ready", "Detonador listo"),
            LeagueLine.Detonated => T("Explosives Detonated", "Explosivos detonados"),
            LeagueLine.DeliriumBegan => T("Delirium Mirror activated: stay inside the fog", "Espejo de delirio activado: quédate dentro de la niebla"),
            LeagueLine.InFog => T("In the fog", "En la niebla"),
            LeagueLine.OutOfFog => T("Out of the fog", "Fuera de la niebla"),
            LeagueLine.SimulacrumWave => T($"Wave {wave} of {LeaguePieces.SimulacrumWaves}", $"Oleada {wave} de {LeaguePieces.SimulacrumWaves}"),
            LeagueLine.SimulacrumWaveCleared => T("Wave cleared; the next starts at the mirror", "Oleada superada; la siguiente empieza en el espejo"),
            LeagueLine.SimulacrumOver => T("Simulacrum over", "Simulacro terminado"),
            LeagueLine.BreachOpen => T("Breach open", "Fisura abierta"),
            LeagueLine.BreachStabilised => T("Breach Stabilised: stronger monsters from the centre", "Fisura estabilizada: monstruos más fuertes desde el centro"),
            LeagueLine.StableBreachCollapsed => T("Stable Breach Collapsed", "Fisura estable colapsada"),
            LeagueLine.UnstableBreachCollapsed => T("Unstable Breach Collapsed", "Fisura inestable colapsada"),
            LeagueLine.StrongboxAmbush => T($"{StrongboxName(name)}: ambush", $"{StrongboxName(name)}: emboscada"),
            LeagueLine.StrongboxReady => T($"{StrongboxName(name)}: can be opened", $"{StrongboxName(name)}: ya se puede abrir"),
            LeagueLine.RogueExileFight => name.Length > 0 ? T($"{name}, rogue exile", $"{name}, exiliado renegado") : T("Rogue exile", "Exiliado renegado"),
            LeagueLine.BeaconReady => T("Vaal Beacon ready", "Foco vaal listo"),
            LeagueLine.BeaconDone => T("Vaal Beacon complete", "Foco vaal completado"),
            LeagueLine.BeaconPortal => T("Portal to the Vaal Ruins open", "Portal a las Ruinas vaal abierto"),
            LeagueLine.RitualDone => T("Ritual complete", "Ritual completado"),
            _ => T("Abyss closed", "Abismo cerrado"),
        };

        /// <summary>A monster an Azmeri spirit has reached, by the game's name of it and of the buff.</summary>
        public static string Possessed(string name) => name.Length > 0
            ? T($"{name}, Possessed by an Azmeri Spirit", $"{name}, poseído por un espíritu azmeri")
            : T("A monster is Possessed by an Azmeri Spirit", "Un monstruo está poseído por un espíritu azmeri");

        /// <summary>The shrine just used, by its name and what its buff says (Shrines and BuffDefinitions).</summary>
        public static string Shrine(ShrineKind shrine) => shrine switch
        {
            ShrineKind.Meteoric => T(
                "Meteoric Shrine. You gain Damage as Fire Damage and Meteors rain down around you.",
                "Santuario de meteoritos. Ganas el daño como daño de fuego y caen meteoritos a tu alrededor."),
            ShrineKind.Tempest => T(
                "Tempest Shrine. You gain Damage as Lightning Damage and bolts strike the ground around you.",
                "Santuario de tempestad. Ganas el daño como daño de rayo y unas descargas golpean el suelo a tu alrededor."),
            ShrineKind.Freezing => T(
                "Freezing Shrine. You gain Damage as Cold Damage and freezing shards fire around you in a spiral.",
                "Santuario de congelamiento. Ganas el daño como daño de hielo y unas esquirlas congelantes se disparan a tu alrededor en forma de espiral."),
            ShrineKind.Gloom => T(
                "Gloom Shrine. You gain Damage as Chaos Damage and slain enemies can explode dealing Chaos Damage based on their maximum Life.",
                "Santuario de penumbra. Ganas el daño como daño de caos, y los enemigos derrotados pueden explotar para infligir daño de caos en función de su vida máxima."),
            ShrineKind.Acceleration => T(
                "Acceleration Shrine. You have increased Skill and Movement Speed.",
                "Santuario de aceleración. Tienes las velocidades de habilidades y movimiento aumentadas."),
            ShrineKind.Divine => T("Divine Shrine. You cannot be Damaged.", "Santuario divino. No te pueden infligir daño."),
            ShrineKind.Seeking => T(
                "Seeking Shrine. You have increased Rarity of Items Found.",
                "Santuario de búsqueda. Tienes rareza de objetos encontrados aumentada."),
            ShrineKind.Enduring => T("Enduring Shrine. You have increased Life and Defences.", "Santuario de fortaleza. Tienes vida y defensas aumentadas."),
            ShrineKind.Replenishing => T("Replenishing Shrine. You are regenerating Life and Mana.", "Santuario de recarga. Regeneras vida y maná."),
            ShrineKind.Enlightening => T("Enlightening Shrine. You gain increased Experience.", "Santuario de instrucción. Ganas experiencia aumentada."),
            ShrineKind.Greed => T("Greed Shrine. Monsters you kill only drop gold.", "Santuario de avaricia. Los monstruos que matas solo arrojan oro."),
            ShrineKind.Resistance => T(
                "Resistance Shrine. You have additional Resistance to Elemental Damage.",
                "Santuario de resistencia. Tienes resistencia adicional al daño elemental."),
            ShrineKind.Diamond => T("Diamond Shrine. You always deal Critical Hits.", "Santuario de diamante. Siempre infliges impactos críticos."),
            ShrineKind.Corrupting => T("Corrupting Shrine. All item drops are Corrupted.", "Santuario corruptor. Todos los objetos que caen están corruptos."),
            _ => T(
                "Greed Shrine. All Equipment found converted to other items.",
                "Santuario de avaricia. Todo el equipamiento que cae se convierte en otros objetos."),
        };

        /// <summary>"Covers 2 chests, 1 elite": what the Expedition charge being placed would blow.</summary>
        public static string ExpeditionCovers(IReadOnlyList<(ExpeditionFind Find, int Count)> finds)
        {
            if (finds.Count == 0)
            {
                return T("Covers nothing", "No cubre nada");
            }

            var list = string.Join(", ", finds.Select(find => FindCount(find.Find, find.Count)));
            return T($"Covers {list}", $"Cubre {list}");
        }

        /// <summary>The Expedition charge being placed cannot go where it stands.</summary>
        public static string ExplosiveRefused => T("Cannot be placed here", "No se puede colocar aquí");

        /// <summary>The HUD's count of the charges set ("Explosives Placed: {0}/{1}"), without the most it does not tell us.</summary>
        public static string ExplosivesPlaced(int count) => T($"Explosives Placed: {count}", $"Explosivos colocados: {count}");

        private static string StrongboxName(string name) => name.Length > 0 ? name : T("Strongbox", "Caja fuerte");

        private static string FindCount(ExpeditionFind find, int count) => (find, count == 1) switch
        {
            (ExpeditionFind.Chest, true) => T("1 chest", "1 cofre"),
            (ExpeditionFind.Chest, false) => T($"{count} chests", $"{count} cofres"),
            (ExpeditionFind.Elite, true) => T("1 elite", "1 élite"),
            (ExpeditionFind.Elite, false) => T($"{count} elites", $"{count} élites"),
            (ExpeditionFind.Monsters, true) => T("1 monster", "1 monstruo"),
            (ExpeditionFind.Monsters, false) => T($"{count} monsters", $"{count} monstruos"),
            (ExpeditionFind.SpecialMonsters, true) => T("1 special monster", "1 monstruo especial"),
            (ExpeditionFind.SpecialMonsters, false) => T($"{count} special monsters", $"{count} monstruos especiales"),
            (ExpeditionFind.Boss, true) => T("1 boss", "1 jefe"),
            (ExpeditionFind.Boss, false) => T($"{count} bosses", $"{count} jefes"),
            (ExpeditionFind.Relic, true) => T("1 relic", "1 reliquia"),
            (ExpeditionFind.Relic, false) => T($"{count} relics", $"{count} reliquias"),
            (_, true) => T("1 remnant", "1 remanente"),
            _ => T($"{count} remnants", $"{count} remanentes"),
        };
    }
}

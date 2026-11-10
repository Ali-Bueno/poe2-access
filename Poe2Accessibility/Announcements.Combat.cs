namespace Poe2Accessibility
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Spoken strings of combat state: boss life, skills ready, culling, charges, rage, enemy states, weak points and ailments.</summary>
    public static partial class Announcements
    {
        /// <summary>A boss's life fell past a step: "The Bloated Miller 40 percent".</summary>
        public static string BossLife(string name, int percent) => T($"{name} {percent} percent", $"{name} {percent} por ciento");

        public static string BossDefeated(string name) => T($"{name} defeated", $"{name} derrotado");

        /// <summary>A skill whose cooldown ran out: "Granada explosiva disponible".</summary>
        public static string SkillReady(string name) => T($"{name} ready", $"{name} disponible");

        /// <summary>A rare or unique enemy now within culling range: "Beira, de la manada putrefacta: golpe de gracia".</summary>
        public static string Cullable(string name) => T($"{name}: culling strike", $"{name}: golpe de gracia");

        /// <summary>
        ///     What the player holds now, in the game's words: "3 cargas de poder", "sin cargas de aguante"; the others by
        ///     name and count ("Infusión de fuego: 2"), Arcane Surge by name alone.
        /// </summary>
        public static string Charges(Services.ChargeKind kind, int count) => (kind, count) switch
        {
            (Services.ChargeKind.Power, 0) => T("no power charges", "sin cargas de poder"),
            (Services.ChargeKind.Power, 1) => T("1 power charge", "1 carga de poder"),
            (Services.ChargeKind.Power, _) => T($"{count} power charges", $"{count} cargas de poder"),
            (Services.ChargeKind.Frenzy, 0) => T("no frenzy charges", "sin cargas de frenesí"),
            (Services.ChargeKind.Frenzy, 1) => T("1 frenzy charge", "1 carga de frenesí"),
            (Services.ChargeKind.Frenzy, _) => T($"{count} frenzy charges", $"{count} cargas de frenesí"),
            (Services.ChargeKind.Endurance, 0) => T("no endurance charges", "sin cargas de aguante"),
            (Services.ChargeKind.Endurance, 1) => T("1 endurance charge", "1 carga de aguante"),
            (Services.ChargeKind.Endurance, _) => T($"{count} endurance charges", $"{count} cargas de aguante"),
            (Services.ChargeKind.Spirit, 0) => T("no spirit charges", "sin cargas de espíritu"),
            (Services.ChargeKind.Spirit, 1) => T("1 spirit charge", "1 carga de espíritu"),
            (Services.ChargeKind.Spirit, _) => T($"{count} spirit charges", $"{count} cargas de espíritu"),
            (Services.ChargeKind.ArcaneSurge, 0) => T("Arcane Surge ended", "Sobrecarga arcana terminada"),
            (Services.ChargeKind.ArcaneSurge, _) => ChargeName(kind),
            (Services.ChargeKind.Combo, 0) => T("no Combo", "sin combo"),
            _ => T($"{ChargeName(kind)}: {count}", $"{ChargeName(kind)}: {count}"),
        };

        /// <summary>The game's name for a resource counted by name (BuffDefinitions, read 2026-10-29).</summary>
        public static string ChargeName(Services.ChargeKind kind) => kind switch
        {
            Services.ChargeKind.FireInfusion => T("Fire Infusion", "Infusión de fuego"),
            Services.ChargeKind.ColdInfusion => T("Cold Infusion", "Infusión de hielo"),
            Services.ChargeKind.LightningInfusion => T("Lightning Infusion", "Infusión de rayo"),
            Services.ChargeKind.FireSurge => T("Fire Surge", "Sobrecarga de fuego"),
            Services.ChargeKind.ColdSurge => T("Cold Surge", "Sobrecarga de hielo"),
            Services.ChargeKind.LightningSurge => T("Lightning Surge", "Sobrecarga de rayo"),
            Services.ChargeKind.ChaosSurge => T("Chaos Surge", "Sobrecarga de caos"),
            Services.ChargeKind.DruidicProwess => T("Druidic Prowess", "Proeza druídica"),
            Services.ChargeKind.ArcaneSurge => T("Arcane Surge", "Sobrecarga arcana"),
            Services.ChargeKind.Combo => T("Combo", "Combo"),
            _ => T("Rage", "Furia"),
        };

        /// <summary>Rage reaching its maximum: "Furia al máximo".</summary>
        public static string RageFull => T("Rage full", "Furia al máximo");

        public static string RageEmpty => T("no Rage", "sin furia");

        /// <summary>Rage in the vitals report: "Furia 18 de 30".</summary>
        public static string Rage(int count, int max) => T($"Rage {count} of {max}", $"Furia {count} de {max}");

        /// <summary>An enemy state's name, as the game's KeywordPopups call it (read 2026-10-26).</summary>
        public static string EnemyStateName(Services.EnemyState state) => state switch
        {
            Services.EnemyState.PrimedForStun => T("Primed for Stun", "Listo para el aturdimiento"),
            Services.EnemyState.HeavyStunned => T("Heavy Stun", "Aturdimiento total"),
            Services.EnemyState.Frozen => T("Frozen", "Congelado"),
            Services.EnemyState.Electrocuted => T("Electrocution", "Parálisis"),
            Services.EnemyState.Pinned => T("Pinned", "Retenido"),
            Services.EnemyState.ArmourBroken => T("Armour Break", "Rotura de armadura"),
            Services.EnemyState.Dazed => T("Daze", "Desorientación"),
            _ => T("Parried", "Parado"),
        };

        /// <summary>A rare or unique enemy has just entered states: "Beira, de la manada putrefacta: aturdimiento total".</summary>
        public static string EnemyEntered(string name, IEnumerable<Services.EnemyState> states)
        {
            var named = string.Join(", ", states.Select(EnemyStateName));
            return T($"{name}: {named}", $"{name}: {named}");
        }

        /// <summary>The enemy state a sound glossary key ("state_Frozen") stands for.</summary>
        public static Services.EnemyState? StateOfSound(string key) =>
            key.StartsWith("state_", StringComparison.Ordinal) && Enum.TryParse<Services.EnemyState>(key["state_".Length..], out var state) ? state : null;

        /// <summary>Once per area, as the weak-point heartbeat first leads to a boss's head.</summary>
        public static string WeakPointGuide(string name) =>
            T($"{name}: strike the head, follow the heartbeat", $"{name}: golpea la cabeza, sigue el latido");

        /// <summary>"Poisoned" as it begins (D2R: high priority, cannot be cut off).</summary>
        public static string AilmentBegins(Services.StatusAilment ailment) => ailment switch
        {
            Services.StatusAilment.StopMoving => T("Stop moving!", "¡Quédate quieto!"),
            Services.StatusAilment.KeepMoving => T("Keep moving!", "¡Sigue moviéndote!"),
            _ => AilmentName(ailment),
        };

        /// <summary>"No longer poisoned" once it has been gone for half a second.</summary>
        public static string AilmentEnds(Services.StatusAilment ailment) => ailment switch
        {
            Services.StatusAilment.StopMoving or Services.StatusAilment.KeepMoving => T("You may move freely", "Ya puedes moverte libremente"),
            Services.StatusAilment.Overheated => T("Weapon ready", "Arma lista"),
            Services.StatusAilment.HonourWeakness => T("Honour weakness gone", "Se acabó la debilidad de resolución"),
            Services.StatusAilment.BindingChains => T("Chains gone", "Se soltaron las cadenas"),
            _ => T($"No longer {AilmentName(ailment).ToLowerInvariant()}", $"Ya no estás {AilmentName(ailment).ToLowerInvariant()}"),
        };

        public static string AilmentName(Services.StatusAilment ailment) => ailment switch
        {
            Services.StatusAilment.Frozen => T("Frozen", "Congelado"),
            Services.StatusAilment.Chilled => T("Chilled", "Enfriado"),
            Services.StatusAilment.Shocked => T("Shocked", "Electrizado"),
            Services.StatusAilment.Electrocuted => T("Electrocuted", "Electrocutado"),
            Services.StatusAilment.Ignited => T("Ignited", "En llamas"),
            Services.StatusAilment.Poisoned => T("Poisoned", "Envenenado"),
            Services.StatusAilment.Bleeding => T("Bleeding", "Sangrando"),
            Services.StatusAilment.Stunned => T("Stunned", "Aturdido"),
            Services.StatusAilment.Pinned => T("Pinned", "Inmovilizado"),
            Services.StatusAilment.Slowed => T("Slowed", "Ralentizado"),
            Services.StatusAilment.Maimed => T("Maimed", "Mutilado"),
            Services.StatusAilment.Blinded => T("Blinded", "Cegado"),
            Services.StatusAilment.Overheated => T("Weapon overheated", "Arma sobrecalentada"),
            Services.StatusAilment.HonourWeakness => T("Honour weakness: 50% more honour lost", "Debilidad de resolución: pierdes un 50% más de honor"),
            Services.StatusAilment.BindingChains => T("Binding chains", "Cadenas apresantes"),
            _ => T("Cursed", "Maldito"),
        };
    }
}

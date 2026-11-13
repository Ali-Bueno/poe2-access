namespace Poe2Accessibility
{
    using System.Collections.Generic;

    /// <summary>The vitals and flask phrases of <see cref="Announcements"/>: life, mana, energy shield, stun and flasks.</summary>
    public static partial class Announcements
    {
        /// <summary>Mana fell below a threshold: "Mana 25 percent".</summary>
        public static string LowMana(int percent) => T($"Mana {percent} percent", $"Maná {percent} por ciento");

        /// <summary>Energy shield fell below a threshold: "Energy shield 50 percent".</summary>
        public static string LowEnergyShield(int percent) => T($"Energy shield {percent} percent", $"Escudo de energía {percent} por ciento");

        public static string EnergyShieldGone => T("Energy shield depleted", "Escudo de energía agotado");

        /// <summary>The player's heavy stun bar rose past a step while blocking or parrying (the game's "stun bar"): "Stun bar 80 percent".</summary>
        public static string StunBar(int percent) => T($"Stun bar {percent} percent", $"Barra de aturdimiento {percent} por ciento");

        /// <summary>
        ///     A flask as it stands, for the vitals readout and after a drink: "Frasco de vida menor, 2 usos"; by its charges
        ///     while what a drink of it costs has not been seen (<paramref name="uses"/> null): "Frasco de vida menor, 71 cargas".
        /// </summary>
        public static string Flask(string name, int charges, int? uses) => (charges, uses) switch
        {
            (<= 0, _) or (_, <= 0) => FlaskEmpty(name),
            (_, 1) => T($"{name}, 1 use", $"{name}, 1 uso"),
            (_, { } left) => T($"{name}, {left} uses", $"{name}, {left} usos"),
            (1, _) => T($"{name}, 1 charge", $"{name}, 1 carga"),
            _ => T($"{name}, {charges} charges", $"{name}, {charges} cargas"),
        };

        /// <summary>A flask with no use left: "Frasco de vida menor vacío".</summary>
        public static string FlaskEmpty(string name) => T($"{name} empty", $"{name} vacío");

        /// <summary>A flask that gained a use from kills or time: "Frasco de vida menor recargado, 2 usos".</summary>
        public static string FlaskRefilled(string name, int uses) => uses == 1
            ? T($"{name} refilled, 1 use", $"{name} recargado, 1 uso")
            : T($"{name} refilled, {uses} uses", $"{name} recargado, {uses} usos");

        /// <summary>An empty flask filled again before any drink showed what one costs: by its charges.</summary>
        public static string FlaskRefilledCharges(string name, int charges) => T($"{name} refilled, {charges} charges", $"{name} recargado, {charges} cargas");

        /// <summary>A flask put on the belt during play: "Nuevo frasco: Frasco de vida mayor, 3 usos".</summary>
        public static string FlaskArrived(string flask) => T($"New flask: {flask}", $"Nuevo frasco: {flask}");

        /// <summary>"Life 80 percent, mana 40 percent, energy shield 100 percent" (energy shield only when the character has some).</summary>
        public static string Vitals(int life, int mana, int? energyShield)
        {
            var parts = new List<string>
            {
                T($"Life {life} percent", $"Vida {life} por ciento"),
                T($"mana {mana} percent", $"maná {mana} por ciento"),
            };
            if (energyShield is { } shield)
            {
                parts.Add(T($"energy shield {shield} percent", $"escudo de energía {shield} por ciento"));
            }

            return string.Join(", ", parts);
        }
    }
}

namespace Poe2Accessibility
{
    using System.Collections.Generic;

    /// <summary>Spoken strings of the guide beacon, vitals, target and text fields (same translatable layer).</summary>
    public static partial class Announcements
    {
        /// <summary>"Guiding to Clearfell, 116 steps, down 3, then left" (the scanner's route description).</summary>
        public static string BeaconLocked(string described) => T($"Guiding to {described}", $"Guiando hacia {described}");

        public static string BeaconOff => T("Guide off", "Guía desactivada");

        public static string BeaconArrived(string name) => T($"Arrived at {name}", $"Llegaste a {name}");

        public static string BeaconNoRoute(string name) => T($"No route to {name} yet", $"Todavía no hay ruta hacia {name}");

        public static string BeaconNeedsChoice => T("Choose something with the scanner first", "Primero elige algo con el escáner");

        /// <summary>Mana fell below a threshold: "Mana 25 percent".</summary>
        public static string LowMana(int percent) => T($"Mana {percent} percent", $"Maná {percent} por ciento");

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

        /// <summary>The text field closed: what it holds now, so the player knows editing ended.</summary>
        public static string EditFinished(string value) => value.Length > 0
            ? T($"{value}, editing done", $"{value}, edición terminada")
            : T("Editing done, field empty", "Edición terminada, campo vacío");

        /// <summary>A boss's life fell past a step: "The Bloated Miller 40 percent".</summary>
        public static string BossLife(string name, int percent) => T($"{name} {percent} percent", $"{name} {percent} por ciento");

        public static string BossDefeated(string name) => T($"{name} defeated", $"{name} derrotado");

        /// <summary>"Poisoned" as it begins (D2R: high priority, cannot be cut off).</summary>
        public static string AilmentBegins(Services.StatusAilment ailment) => AilmentName(ailment);

        /// <summary>"No longer poisoned" once it has been gone for half a second.</summary>
        public static string AilmentEnds(Services.StatusAilment ailment) =>
            T($"No longer {AilmentName(ailment).ToLowerInvariant()}", $"Ya no estás {AilmentName(ailment).ToLowerInvariant()}");

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
            _ => T("Cursed", "Maldito"),
        };

        /// <summary>The thing the right stick aims at: "Drowned", "Bloated Miller, unique, 40 percent".</summary>
        public static string Target(string name, int? lifePercent) => lifePercent is { } life and < 100
            ? T($"{name}, {life} percent", $"{name}, {life} por ciento")
            : name;
    }
}

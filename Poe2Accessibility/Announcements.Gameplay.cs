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

        /// <summary>Spoken name of a setting of the settings menu, by field name.</summary>
        public static string SettingName(string field) => field switch
        {
            nameof(AccessibilitySettings.AnnounceTarget) => T("Target names", "Nombres de objetivos"),
            nameof(AccessibilitySettings.AnnounceLowLife) => T("Low life warnings", "Avisos de vida baja"),
            nameof(AccessibilitySettings.AnnounceLowMana) => T("Low mana warnings", "Avisos de maná bajo"),
            nameof(AccessibilitySettings.AnnounceBossLife) => T("Boss life", "Vida de jefes"),
            nameof(AccessibilitySettings.StatusAlerts) => T("Harmful states", "Estados alterados"),
            nameof(AccessibilitySettings.AnnounceNearby) => T("Name nearby things", "Nombrar cosas cercanas"),
            nameof(AccessibilitySettings.AnnounceChat) => T("Chat", "Chat"),
            nameof(AccessibilitySettings.ReadTooltipsOnFocus) => T("Whole tooltips on focus", "Descripciones completas al enfocar"),
            nameof(AccessibilitySettings.AnnouncePickups) => T("Picked up items", "Objetos recogidos"),
            nameof(AccessibilitySettings.AnnounceLevelUp) => T("Level up", "Subida de nivel"),
            nameof(AccessibilitySettings.Radar) => T("Radar", "Radar"),
            nameof(AccessibilitySettings.EnemySounds) => T("Enemy sounds", "Sonidos de enemigos"),
            nameof(AccessibilitySettings.NpcSounds) => T("NPC sounds", "Sonidos de personajes"),
            nameof(AccessibilitySettings.WallBump) => T("Wall bump", "Choque con paredes"),
            nameof(AccessibilitySettings.ObjectSounds) => T("Object sounds", "Sonidos de objetos"),
            nameof(AccessibilitySettings.DodgeWarnings) => T("Dodge warnings", "Avisos para esquivar"),
            nameof(AccessibilitySettings.CrosshairBeep) => T("Crosshair beep", "Pitido de mira"),
            nameof(AccessibilitySettings.LowLifeHeartbeat) => T("Low life heartbeat", "Latido de vida baja"),
            nameof(AccessibilitySettings.LowLifeBelow) => T("Heartbeat below", "Latido por debajo de"),
            nameof(AccessibilitySettings.SoundVolume) => T("Master volume", "Volumen general"),
            nameof(AccessibilitySettings.RadarVolume) => T("Radar volume", "Volumen del radar"),
            nameof(AccessibilitySettings.EnemyVolume) => T("Enemy volume", "Volumen de enemigos"),
            nameof(AccessibilitySettings.BeaconVolume) => T("Guide volume", "Volumen de la guía"),
            nameof(AccessibilitySettings.ObjectVolume) => T("Object volume", "Volumen de objetos"),
            nameof(AccessibilitySettings.HeartbeatVolume) => T("Heartbeat volume", "Volumen del latido"),
            nameof(AccessibilitySettings.Language) => T("Language", "Idioma"),
            _ => field,
        };

        /// <summary>"Recogido: Traje de ermitaño"; a stack that grew: "Recogido 2 más: Pergamino de sabiduría, 5".</summary>
        public static string PickedUp(string name, int added) => added > 0
            ? T($"Picked up {added} more: {name}", $"Recogido {added} más: {name}")
            : T($"Picked up: {name}", $"Recogido: {name}");

        public static string SettingsMenuTitle => T("Mod settings", "Ajustes del mod");

        public static string SettingsMenuClosed => T("Settings saved", "Ajustes guardados");

        /// <summary>"Sonidos de objetos, activado, 14 de 25".</summary>
        public static string SettingLine(string name, string value, int position, int count) => $"{name}, {value}, {ListPosition(position, count)}";

        public static string On => T("on", "activado");

        public static string Off => T("off", "desactivado");

        public static string Percent(int value) => T($"{value} percent", $"{value} por ciento");

        public static string LanguageName(string code) => code switch
        {
            "es" => "Español",
            "en" => "English",
            _ => T("Same as the game", "Igual que el juego"),
        };

        /// <summary>The thing the right stick aims at: "Drowned", "Bloated Miller, unique, 40 percent".</summary>
        public static string Target(string name, int? lifePercent) => lifePercent is { } life and < 100
            ? T($"{name}, {life} percent", $"{name}, {life} por ciento")
            : name;
    }
}

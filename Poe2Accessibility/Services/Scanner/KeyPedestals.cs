namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     Stone altars that take a quest item: the game's own `KeyPedestal` objects (Jiquani's Machinarium's four for Small
    ///     Soul Cores, `Metadata/Terrain/Gallows/Act3/3_6_1/Objects/KeyPedestal_*`; the Molten Vault's for Frost Cores,
    ///     `…/3_9/Objects/KeyPedestal_02`). The Molten Vault's quest names neither the core nor the altar ("Despeja la Cámara
    ///     fundida"), so the altar went unheard with the core in hand (the owner, 2026-11-19). A used one leaves play. Pure.
    /// </summary>
    public static class KeyPedestals
    {
        private const string PathPart = "/KeyPedestal";

        /// <summary>An altar that takes a quest item; it sounds as the quest's while the player carries one.</summary>
        public static bool Is(string path) => path.Contains(PathPart, StringComparison.OrdinalIgnoreCase);
    }
}

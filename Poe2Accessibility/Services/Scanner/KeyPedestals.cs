namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

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

        /// <summary>
        ///     An object the game names after a quest item the player carries ("Colocar corazón sacrificial" with the
        ///     Corazón sacrificial in the bag, Aggorat): where that item is used.
        /// </summary>
        public static bool TakesCarried(string name, IEnumerable<string> carried) =>
            name.Length > 0 && carried.Any(item => item.Length > 0 && !string.Equals(item, name, StringComparison.OrdinalIgnoreCase) &&
                                                   name.Contains(item, StringComparison.OrdinalIgnoreCase));
    }
}

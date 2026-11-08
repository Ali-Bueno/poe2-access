namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     Whether the area is a town or a hideout, from its WorldAreas row: the row's town flag, or an Id naming a hideout (not
    ///     a hideout's map). Read from the row the loading state holds (<see cref="GameStateService"/>): the host's WorldData
    ///     chain reached no row in 0.5.5f (its second hop, +0x98, reads 0), so in town the area's corner was said and the lost
    ///     hint and map hints were not held back (field audit A4, live 2026-11-08).
    /// </summary>
    public static class AreaKind
    {
        public static bool IsTownOrHideout(string id, bool townFlag) =>
            townFlag || (id.Contains("hideout", StringComparison.OrdinalIgnoreCase) && !id.Contains("map", StringComparison.OrdinalIgnoreCase));
    }
}

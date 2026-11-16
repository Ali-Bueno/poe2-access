namespace Poe2Accessibility.Services
{
    using System;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     Which entities are a way to another area. Most lie under `AreaTransitions/`, but a level's own object can be
    ///     one: the Mawdun Quarry's crane down to the Mawdun Mine is
    ///     `Metadata/Terrain/Gallows/Act2/2_10_1/Objects/QuarryCrane_ExitPortal`, with an AreaTransition component naming
    ///     G2_10_2 and no Portal component (entity dump, 2026-11-16). Its path's "Portal" made it a portal: "Portal: Mina
    ///     Mawdun", and the exit was listed twice. A player's or the map device's portal has a Portal component and no
    ///     AreaTransition (MultiplexPortal, dumps of 2026-11-05 and 11-07).
    /// </summary>
    public static class ExitEntities
    {
        private const string PathMarker = "AreaTransition", TransitionComponent = "AreaTransition", PortalComponent = "Portal";

        public static bool IsExit(Entity entity, string path) =>
            path.Contains(PathMarker, StringComparison.Ordinal) || (entity.HasComponent(TransitionComponent) && !entity.HasComponent(PortalComponent));
    }
}

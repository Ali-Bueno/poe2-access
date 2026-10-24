namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameHelper;
    using GameHelper.RemoteObjects.Components;
    using GameOffsets.Natives;

    /// <summary>
    ///     The game names no checkpoint: on its travel map a sighted player tells them apart by what lies around each.
    ///     Each marker is matched to a checkpoint the player has seen (<see cref="MapPlaces.Assign"/>) and said by the
    ///     named place beside it and how far it is ("Viajar al punto de control, cerca de Campamento de Sierraclara,
    ///     abajo, a 35 pasos").
    /// </summary>
    public sealed class CheckpointPlaces
    {
        /// <summary>A named place this near (world units, about 20 steps) is what the checkpoint is beside.</summary>
        public const float NearPlaceWorld = 2000f;

        /// <summary>The player stands at a checkpoint when this near it (the map opens from one).</summary>
        private const float AtCheckpointWorld = 500f;

        /// <summary>One spoken step, as the scanner counts them.</summary>
        private const float StepWorldUnits = 100f;

        private readonly EntityCatalog catalog;

        public CheckpointPlaces(EntityCatalog catalog)
        {
            this.catalog = catalog;
        }

        /// <summary>
        ///     For each marker (screen offset from the player's own, which is <paramref name="here"/>) the named place beside
        ///     its checkpoint and the steps to it (0 for the player's own); null when no seen checkpoint fits it.
        /// </summary>
        public IReadOnlyList<(string? Near, int Steps)?> Describe(IReadOnlyList<Vector2> markers, int here)
        {
            var result = new (string? Near, int Steps)?[markers.Count];
            if (!Core.States.InGameStateObject.CurrentAreaInstance.Player.TryGetComponent<Render>(out var player))
            {
                return result;
            }

            var entries = this.catalog.Entries();
            var playerAt = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var checkpoints = entries.Where(entry => entry.Category == ScanCategory.Waypoints && entry.Kind == "checkpoint").ToList();
            var own = checkpoints.OrderBy(entry => Vector2.Distance(At(entry), playerAt)).FirstOrDefault();
            if (own != null && Vector2.Distance(At(own), playerAt) > AtCheckpointWorld)
            {
                own = null;
            }

            (string? Near, int Steps) Place(ScanEntry checkpoint, bool withDistance) => (
                MapPlaces.NamedNear(At(checkpoint), entries, NearPlaceWorld)?.Name,
                withDistance ? (int)Math.Round(Vector2.Distance(At(checkpoint), playerAt) / StepWorldUnits) : 0);

            if (own != null && here >= 0 && here < markers.Count)
            {
                result[here] = Place(own, false);
            }

            var origin = own == null ? playerAt : At(own);
            var originTuple = new StdTuple3D<float> { X = origin.X, Y = origin.Y, Z = player.WorldPosition.Z };
            var others = checkpoints.Where(entry => entry != own).ToList();
            var markerIndexes = Enumerable.Range(0, markers.Count).Where(index => index != here).ToList();
            var pairs = MapPlaces.Assign(
                markerIndexes.Select(index => markers[index]).ToList(),
                others.Select(entry => ScreenProjection.Direction(originTuple, At(entry) - origin)).ToList());
            for (var i = 0; i < pairs.Length; i++)
            {
                if (pairs[i] >= 0)
                {
                    result[markerIndexes[i]] = Place(others[pairs[i]], true);
                }
            }

            return result;
        }

        private static Vector2 At(ScanEntry entry) => new(entry.WorldX, entry.WorldY);
    }
}

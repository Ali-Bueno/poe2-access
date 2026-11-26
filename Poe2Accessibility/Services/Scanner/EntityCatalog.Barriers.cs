namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteEnums;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     The scanner's ground past closed barriers: the entries for it, the gate's state near the guide, the boss on the
    ///     player's side, and the barriers found shut (docs/superpowers/specs/2026-11-26-ground-behind-closed-barriers-design.md).
    /// </summary>
    public sealed partial class EntityCatalog
    {
        /// <summary>A barrier entry stands one cell from its gate: a cell held closed this near it (each axis) is that gate.</summary>
        private const int GateCells = 4;

        /// <summary>The barriers the guide found shut, kept out of the exploring guide until something changes.</summary>
        private readonly TriedBarriers tried = new();

        /// <summary>Whether the ground past a closed barrier is listed among the unexplored (setting GroundBehindBarriers).</summary>
        public bool GroundBehindBarriers { get; set; } = true;

        /// <summary>Whether the barrier memory holds a cell closed by a grid cell: the gate a barrier entry stands at is still shut.</summary>
        public bool BarrierClosedNear(Vector2 cell)
        {
            var row = Core.States.InGameStateObject.CurrentAreaInstance.TerrainMetadata.BytesPerRow;
            lock (this.samplesLock)
            {
                return this.grid.ClosedNear((int)cell.X, (int)cell.Y, GateCells, row);
            }
        }

        /// <summary>
        ///     The name of the nearest boss alive on the player's own ground, the game's word for it; null when none is. A boss is
        ///     a living hostile monster of unique rarity among the entities the game reports, on screen or not: what may keep a
        ///     gate shut (the Crowbell, 310 cells from the player in its pen, 2026-11-26).
        /// </summary>
        public string? BossOnPlayersGround()
        {
            if (!Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return null;
            }

            var here = new Vector2(player.GridPosition.X, player.GridPosition.Y);
            string? nearest = null;
            var nearestDistance = float.MaxValue;
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (!entity.IsValid || !IsAliveEnemy(entity) || IsHidden(entity) ||
                    !entity.TryGetComponent<ObjectMagicProperties>(out var magic) || magic.Rarity != Rarity.Unique ||
                    !entity.TryGetComponent<Render>(out var render))
                {
                    continue;
                }

                var cell = new Vector2(render.GridPosition.X, render.GridPosition.Y);
                var distance = Vector2.Distance(cell, here);
                if (distance >= nearestDistance || this.OnPlayersGround(cell) != true ||
                    this.elements.ReadWStringStructAt(render.Address + GameLayout.RenderComponent.NameOffset) is not { Length: > 0 } name)
                {
                    continue;
                }

                (nearest, nearestDistance) = (name, distance);
            }

            return nearest;
        }

        /// <summary>The listed ground behind a barrier with the shortest walk to it, then the nearest; null when none is listed.</summary>
        public ScanEntry? NearestBarrierGround()
        {
            if (!Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return null;
            }

            return this.unexplored.Where(entry => entry.Kind == ExplorationMap.BarrierGroundKind)
                .OrderBy(entry => this.unexploredWalks.GetValueOrDefault(entry.Id, int.MaxValue))
                .ThenBy(entry => Distance(player.WorldPosition, entry))
                .FirstOrDefault();
        }

        /// <summary>The guide found this barrier's gate still shut: the exploring guide leaves it until something changes.</summary>
        public void TriedBarrier(uint id) => this.tried.Tried(id);

        /// <summary>
        ///     A boss fell: every barrier may be tried again, and the line naming the nearest one still listed ("The closed
        ///     barrier is 42 steps away"), said after the boss watch's own "defeated"; null when none is listed.
        /// </summary>
        public string? BarrierAfterDefeat()
        {
            this.tried.Retry();
            return this.NearestBarrierGround() is { } gate && this.DistanceTo(gate) is { } away
                ? Announcements.BarrierAfterDefeat(Math.Max(1, (int)Math.Round(away / StepWorldUnits)))
                : null;
        }

        /// <summary>The cells the barrier memory holds closed, for the frontier to look past; null with the setting off or none held.</summary>
        private byte[]? BarrierMask()
        {
            if (!this.GroundBehindBarriers)
            {
                return null;
            }

            lock (this.samplesLock)
            {
                return this.grid.ClosedMask();
            }
        }
    }
}

namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     The doors the player opens (a targetable TriggerableBlockage, Metadata/MiscellaneousObjects/Door) seen shut in
    ///     this area and not opened since: the ground behind one is out of reach, so no unexplored ground shows it, and
    ///     it is a way still to try (docs/radar.md, doors). Pure.
    /// </summary>
    public sealed class ClosedDoors
    {
        /// <summary>The sound of a shut door and the kind of its entry among the unexplored.</summary>
        public const string Kind = "closed_door";

        private readonly Dictionary<uint, Vector3> shut = new();

        public int Count => this.shut.Count;

        /// <summary>A door seen shut, where it stands (world units).</summary>
        public void Shut(uint id, Vector3 at) => this.shut[id] = at;

        /// <summary>The door opened, or the game took it out of play.</summary>
        public void Forget(uint id) => this.shut.Remove(id);

        /// <summary>A new area.</summary>
        public void Clear() => this.shut.Clear();

        /// <summary>
        ///     Drops the doors the game no longer has where the player stands: one removed as it opened is never seen open
        ///     (<see cref="BarrierMemory.IsGone"/>, grid cells).
        /// </summary>
        public void ForgetGone(Func<uint, bool> awake, Vector2 playerCell, float worldPerCell)
        {
            foreach (var (id, at) in this.shut.ToList())
            {
                // Asked of the near ones only: who is awake is a walk over every entity.
                if (BarrierMemory.IsGone(at.X / worldPerCell, at.Y / worldPerCell, false, playerCell.X, playerCell.Y) && !awake(id))
                {
                    this.shut.Remove(id);
                }
            }
        }

        /// <summary>The doors still shut, nearest to <paramref name="from"/> (world units) first.</summary>
        public IReadOnlyList<(uint Id, Vector3 At)> Nearest(Vector2 from) =>
            this.shut.Select(door => (door.Key, door.Value)).OrderBy(door => Vector2.Distance(from, new Vector2(door.Value.X, door.Value.Y))).ToList();
    }
}

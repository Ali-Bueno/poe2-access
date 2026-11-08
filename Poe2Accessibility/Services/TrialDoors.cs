namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     The Trial of the Sekhemas' room doors: after a room, three stand by the Trial Map and the chosen room's opens. The
    ///     door is nameless, untargetable and absent from the scanner; its StateMachine's "open" state (define_shared_state
    ///     "open; wall;" in SanctumRoomSelectorDoor.ot) tells it apart from a wall. Pure.
    /// </summary>
    public static class TrialDoors
    {
        private const string DoorFile = "Metadata/Terrain/Gallows/Leagues/Sanctum/Objects/SanctumRoomSelectorDoor";
        private const string OpenState = "open";
        private const char InstanceMark = '@';

        /// <summary>Whether an object's path is a room door.</summary>
        public static bool IsDoor(string path)
        {
            var mark = path.IndexOf(InstanceMark);
            return string.Equals(mark < 0 ? path : path[..mark], DoorFile, StringComparison.Ordinal);
        }

        /// <summary>Whether a door's states say it is open.</summary>
        public static bool IsOpen(IEnumerable<(string Name, long Value)> states) =>
            states.Any(state => state.Value > 0 && string.Equals(state.Name, OpenState, StringComparison.Ordinal));
    }

    /// <summary>Which doors opened since the last look, each said once. Pure.</summary>
    public sealed class OpenDoorWatch
    {
        private readonly HashSet<uint> open = new();

        /// <summary>A new area: no door is known.</summary>
        public void Reset() => this.open.Clear();

        /// <summary>The doors open now that were not before.</summary>
        public IReadOnlyList<uint> NewlyOpen(IEnumerable<(uint Id, bool Open)> doors)
        {
            var fresh = new List<uint>();
            foreach (var (id, isOpen) in doors)
            {
                if (!isOpen)
                {
                    this.open.Remove(id);
                }
                else if (this.open.Add(id))
                {
                    fresh.Add(id);
                }
            }

            return fresh;
        }
    }
}

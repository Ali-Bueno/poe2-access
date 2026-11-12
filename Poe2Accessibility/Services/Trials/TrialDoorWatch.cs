namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     Says the room door the Trial opens, with the way to it, and puts the guide on it while nothing else is guided: the
    ///     player chose the room on the map and must now find its door among three.
    /// </summary>
    public sealed class TrialDoorWatch
    {
        private const int PollMs = 500;

        private readonly EntityCatalog entities;
        private readonly Beacon beacon;
        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private readonly OpenDoorWatch doors = new();
        private long lastPoll;
        private bool logged;

        public TrialDoorWatch(EntityCatalog entities, Beacon beacon, IAccessibilityOutputService output, PluginLog log)
        {
            this.entities = entities;
            this.beacon = beacon;
            this.output = output;
            this.log = log;
        }

        /// <summary>A new area: the doors there are not known yet.</summary>
        public void Reset() => this.doors.Reset();

        /// <summary>Per frame; <paramref name="playing"/> false while dead, paused or in a menu; <paramref name="guide"/> to lock the beacon on the door.</summary>
        public void Tick(bool playing, bool guide)
        {
            var now = Environment.TickCount64;
            if (!playing || now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            var seen = new List<(uint Id, bool Open, ScanEntry Entry)>();
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (!entity.IsValid || !TrialDoors.IsDoor(entity.Path ?? string.Empty) || !entity.TryGetComponent<Render>(out var render))
                {
                    continue;
                }

                var states = entity.TryGetComponent<StateMachine>(out var machine)
                    ? machine.States.Select(state => (state.Name, state.Value)).ToList()
                    : new List<(string Name, long Value)>();
                var place = render.WorldPosition;
                var entry = new ScanEntry(entity.Id, ScanCategory.Exits, Announcements.TrialDoor, place.X, place.Y, place.Z);
                seen.Add((entity.Id, TrialDoors.IsOpen(states), entry));
                this.LogOnce(states);
            }

            var fresh = this.doors.NewlyOpen(seen.Select(door => (door.Id, door.Open)));
            foreach (var entry in seen.Where(door => fresh.Contains(door.Id)).Select(door => door.Entry))
            {
                if (guide && this.beacon.Target == null)
                {
                    // Says the door and the way to it. The Trial shuts the way behind the player who walks in: the guide ends there.
                    this.beacon.Lock(entry, endsWhenShut: true);
                }
                else
                {
                    this.output.Speak(Announcements.TrialDoorOpen(this.entities.Describe(entry)), true);
                }
            }
        }

        private void LogOnce(List<(string Name, long Value)> states)
        {
            if (!this.logged)
            {
                this.logged = true;
                this.log.Info($"Trial room door seen: {string.Join(", ", states.Select(state => $"{state.Name}={state.Value}"))}");
            }
        }
    }
}

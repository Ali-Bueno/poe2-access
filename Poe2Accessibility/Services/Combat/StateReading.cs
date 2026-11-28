namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader.RemoteObjects.Components;
    using Entity = GameReader.RemoteObjects.States.InGameStateObjects.Entity;

    /// <summary>Reads of an entity's script states (<see cref="StateMachine"/>).</summary>
    public static class StateReading
    {
        /// <summary>The value of the state of that name; 0 when the entity has no such state or no state machine.</summary>
        public static long Of(Entity entity, string name) =>
            entity.TryGetComponent<StateMachine>(out var machine)
                ? machine.States.Where(state => string.Equals(state.Name, name, StringComparison.OrdinalIgnoreCase)).Select(state => state.Value).FirstOrDefault()
                : 0;

        /// <summary>The state names of the entity's script; empty when it has none or none read.</summary>
        public static IReadOnlyList<string> NamesOf(Entity entity) =>
            entity.TryGetComponent<StateMachine>(out var machine) ? machine.States.Select(state => state.Name).ToList() : Array.Empty<string>();
    }
}

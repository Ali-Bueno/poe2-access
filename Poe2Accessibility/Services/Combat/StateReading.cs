namespace Poe2Accessibility.Services
{
    using System;
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
    }
}

namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     How far an object has gone through its transitions (<see cref="GameLayout.TransitionableComponent"/>): an Essence's
    ///     crystal cracking and shattering, an Abyss fissure opening, its pit. Counted from 1, the object as it is built, to the
    ///     number its file declares (<c>num_states</c>).
    /// </summary>
    public static class TransitionState
    {
        /// <summary>The state and the object's count of them; null without the component, or when the bytes cannot be a state.</summary>
        public static (int State, int Count)? Of(HostMemoryReader memory, Entity entity)
        {
            if (!entity.TryGetComponent<Transitionable>(out var component) ||
                !memory.TryRead<byte>(component.Address + GameLayout.TransitionableComponent.StateOffset, out var state) ||
                !memory.TryRead<IntPtr>(component.Address + GameLayout.TransitionableComponent.TypeOffset, out var type) || !memory.IsValidAddress(type) ||
                !memory.TryRead<byte>(type + GameLayout.TransitionableComponent.TypeCountOffset, out var count))
            {
                return null;
            }

            return Transitions.Judge(state, count);
        }

        /// <summary>
        ///     The states of the object's script with its transition beside them, as <see cref="LeaguePieces.Transition"/>: the league
        ///     rules read both the same way.
        /// </summary>
        public static List<(string Name, long Value)> With(HostMemoryReader memory, Entity entity)
        {
            var states = entity.TryGetComponent<StateMachine>(out var machine)
                ? machine.States.Select(state => (state.Name, state.Value)).ToList()
                : new List<(string Name, long Value)>();
            if (Of(memory, entity) is { } transition)
            {
                states.Add((LeaguePieces.Transition, transition.State));
            }

            return states;
        }
    }
}

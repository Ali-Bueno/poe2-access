namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Reflection;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>A component of an entity the host has no class for, by the name the game gives it (the host's own lookup).</summary>
    public static class EntityComponents
    {
        private static readonly FieldInfo? Map = typeof(Entity).GetField("componentAddresses", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>Where the component is; null when the entity has none of that name.</summary>
        public static IntPtr? AddressOf(Entity entity, string name) =>
            Map?.GetValue(entity) is ConcurrentDictionary<string, IntPtr> components && components.TryGetValue(name, out var address) && address != IntPtr.Zero
                ? address
                : null;
    }
}

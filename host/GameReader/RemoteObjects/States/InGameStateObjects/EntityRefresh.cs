namespace GameReader.RemoteObjects.States.InGameStateObjects
{
    /// <summary>What one update reads of an awake entity that is neither a monster nor a player. Pure.</summary>
    public static class EntityRefresh
    {
        /// <summary>How much of the entity is read.</summary>
        public enum Reads
        {
            /// <summary>Its components keep their last values.</summary>
            Nothing,

            /// <summary>The components something asked for are read again.</summary>
            Components,

            /// <summary>The component map is read again and the entity is typed.</summary>
            Everything,
        }

        /// <summary>
        ///     The reads due. An entity that could not be typed (a barrier, a door, a transition, a ground item, a trap) is
        ///     asked what it is only when <paramref name="typingDue"/>, but its components rest like a typed one's: the
        ///     mod reads their state, and waiting for the next typing attempt served it up to a second old. A typed entity
        ///     that is <paramref name="useless"/> (an opened chest) is not read at all.
        /// </summary>
        public static Reads OfResting(bool untyped, bool typingDue, bool useless, long sinceComponentsMs, int restMs)
        {
            if (typingDue)
            {
                return Reads.Everything;
            }

            if (useless && !untyped)
            {
                return Reads.Nothing;
            }

            return sinceComponentsMs >= restMs ? Reads.Components : Reads.Nothing;
        }
    }
}

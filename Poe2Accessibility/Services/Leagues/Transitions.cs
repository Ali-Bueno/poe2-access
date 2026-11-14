namespace Poe2Accessibility.Services
{
    /// <summary>The bytes of an object's transition judged before they are believed (<see cref="GameLayout.TransitionableComponent"/>). Pure.</summary>
    public static class Transitions
    {
        /// <summary>
        ///     The two bytes as a state and the file's count of them, or null: the game's parser takes 2 to 127 states and starts each
        ///     object at 1, so anything else says the field is not where the exe of 0.5.5f had it.
        /// </summary>
        public static (int State, int Count)? Judge(byte state, byte count) =>
            count is >= 2 and <= 127 && state >= 1 && state <= count ? (state, count) : null;
    }
}

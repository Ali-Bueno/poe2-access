namespace Poe2Accessibility.Services
{
    /// <summary>
    ///     Tells another instance of the area the game was already in: a trial's next room or a new instance from the list
    ///     loads under the same world-area row, and its entity ids start over, so what is kept per entity belongs to the
    ///     instance, not to the row. A respawn reloads the same instance (same hash, ids kept) and is nothing. Pure.
    /// </summary>
    public sealed class AreaInstanceGate
    {
        private string held = string.Empty;

        /// <summary>The area's row changed and the per-area state was made anew, for the instance the game holds now (empty: none yet).</summary>
        public void AreaEntered(string hash) => this.held = hash;

        /// <summary>Per frame in game: true once when the game holds another instance than the one last entered.</summary>
        public bool Changed(string hash)
        {
            if (hash.Length == 0 || hash == this.held)
            {
                return false;
            }

            var first = this.held.Length == 0;
            this.held = hash;
            return !first;
        }
    }
}

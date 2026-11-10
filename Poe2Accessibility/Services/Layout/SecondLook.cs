namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     A part one look found lost is looked at again before it is said: a sweep that met a component being freed, a block
    ///     that did not read, reads "lost" once on a healthy game. Only what two looks in a row found lost is confirmed. Pure.
    /// </summary>
    public sealed class SecondLook
    {
        private HashSet<string> last = new(System.StringComparer.Ordinal);

        /// <summary>The parts lost at this look that the look before found lost too; the others wait for the next look.</summary>
        public IReadOnlySet<string> Confirm(IEnumerable<string> lostNow)
        {
            var now = lostNow.ToHashSet(System.StringComparer.Ordinal);
            var confirmed = now.Where(this.last.Contains).ToHashSet(System.StringComparer.Ordinal);
            this.last = now;
            return confirmed;
        }

        /// <summary>A new game process: nothing was lost before.</summary>
        public void Reset() => this.last.Clear();
    }
}

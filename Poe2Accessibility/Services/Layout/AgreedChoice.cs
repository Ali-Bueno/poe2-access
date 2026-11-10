namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;

    /// <summary>
    ///     A choice made on one thing (which decoding reads a dropdown's entries) proves little: another layout can yield
    ///     text there by chance. It becomes everyone's once a second, different thing makes the same choice; until then each
    ///     thing keeps its own. Pure.
    /// </summary>
    public sealed class AgreedChoice<T>
        where T : struct
    {
        private (long Owner, T Choice)? first;

        /// <summary>Gets the choice two different things agreed on; null until they have.</summary>
        public T? Agreed { get; private set; }

        /// <summary>
        ///     <paramref name="owner"/> made <paramref name="choice"/>; true when that settles it. A different choice by
        ///     another owner takes the first one's place, so two that keep disagreeing never settle.
        /// </summary>
        public bool Note(long owner, T choice)
        {
            if (this.Agreed != null)
            {
                return false;
            }

            if (this.first is { } seen && seen.Owner != owner && EqualityComparer<T>.Default.Equals(seen.Choice, choice))
            {
                this.Agreed = choice;
                return true;
            }

            if (this.first is not { } known || known.Owner != owner || !EqualityComparer<T>.Default.Equals(known.Choice, choice))
            {
                this.first = (owner, choice);
            }

            return false;
        }
    }
}

namespace Poe2Accessibility.Services
{
    /// <summary>What changed in a text field between two reads: characters typed at the end or removed from it.</summary>
    public readonly record struct TextEdit(string Added, string Removed)
    {
        /// <summary>Null when the change is not a pure append or delete at the end (pasted over, cursor moved).</summary>
        public static TextEdit? Between(string before, string after)
        {
            if (after.Length > before.Length && after.StartsWith(before, System.StringComparison.Ordinal))
            {
                return new TextEdit(after[before.Length..], string.Empty);
            }

            if (before.Length > after.Length && before.StartsWith(after, System.StringComparison.Ordinal))
            {
                return new TextEdit(string.Empty, before[after.Length..]);
            }

            return null;
        }
    }
}

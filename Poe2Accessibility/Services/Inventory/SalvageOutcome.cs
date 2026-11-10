namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     What one salvage did to the main inventory: the items gone and what came in their place, told by the
    ///     inventory's contents before and after (names as read, stack counts), so a stack that only grows counts too.
    ///     Pure.
    /// </summary>
    public sealed record SalvageOutcome(IReadOnlyList<string> Salvaged, IReadOnlyList<(string Title, int Count)> Received)
    {
        public bool IsEmpty => this.Salvaged.Count == 0 && this.Received.Count == 0;

        public static SalvageOutcome Read(IReadOnlyList<(string Name, int Count)> before, IReadOnlyList<(string Name, int Count)> after)
        {
            var change = new Dictionary<string, int>();
            var order = new List<string>();
            void Add(string name, int count)
            {
                var title = ItemMoves.Title(name);
                if (!change.ContainsKey(title))
                {
                    order.Add(title);
                    change[title] = 0;
                }

                change[title] += count;
            }

            foreach (var (name, count) in before)
            {
                Add(name, -count);
            }

            foreach (var (name, count) in after)
            {
                Add(name, count);
            }

            var stayed = after.Select(item => item.Name).ToList();
            var salvaged = new List<string>();
            foreach (var (name, _) in before)
            {
                if (!stayed.Remove(name) && change[ItemMoves.Title(name)] < 0)
                {
                    salvaged.Add(name);
                }
            }

            var received = order.Where(title => change[title] > 0).Select(title => (title, change[title])).ToList();
            return new SalvageOutcome(salvaged, received);
        }
    }
}

namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>What a Trial shrine or fountain costs in sacred water: the shared state its script declares. Pure.</summary>
    public static class ShrineCost
    {
        /// <summary>The state's name in the shrine's `.ot` (`interaction_check GetSacredWater() >= interact_cost`, 5 there).</summary>
        public const string StateName = "interact_cost";

        /// <summary>The cost the states hold, or 0 when the object has none (or it reads zero or beyond what a cost can be).</summary>
        public static int Of(IEnumerable<(string Name, long Value)> states)
        {
            foreach (var (name, value) in states)
            {
                if (string.Equals(name, StateName, StringComparison.Ordinal))
                {
                    return value is > 0 and <= int.MaxValue ? (int)value : 0;
                }
            }

            return 0;
        }
    }
}

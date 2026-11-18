namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>A spectre's Spirit cost from its monster's experience multiplier; pure, shared with tools/Poe2UiExtract.</summary>
    public static class SpectreSpirit
    {
        /// <summary>
        ///     The reservation of a monster with this experience multiplier: 50 at a multiplier of 100, scaling with
        ///     its 0.75 power (100 gives 50, 110 gives 53, 70 gives 38, 200 gives 84). The curve is the game's since
        ///     0.3.0 rebalanced spectre costs (Path of Building's minion export has it in this form; the round tens
        ///     published before 0.3.0 no longer hold), and it is the cost the bound gem reads before the player's
        ///     own reservation efficiency. Null for a multiplier worth nothing, which no bindable monster has.
        /// </summary>
        public static int? For(int experience)
        {
            if (experience <= 0)
            {
                return null;
            }

            var spirit = (int)Math.Floor(Math.Pow(experience / 100.0, 0.75) * 50);
            return spirit > 0 ? spirit : null;
        }
    }
}

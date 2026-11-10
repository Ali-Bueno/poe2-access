namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;

    /// <summary>
    ///     Buff ids as GameHelper keys them: a definition id, sometimes followed by "_" and the granting skill's number in upper
    ///     case hex ("_79", "_1A"). A definition id can end the same way ("shrine_fire_2" beside "shrine_fire"), so a table is
    ///     asked for the whole key first (<see cref="In"/>). Pure.
    /// </summary>
    public static class BuffIds
    {
        /// <summary>The game's mark on a monster set aside: Count Geonor left behind while the Wolf fights on (read live, 2026-11-05).</summary>
        public const string HiddenMonster = "hidden_monster";

        /// <summary>The key without a trailing "_" and hex digits as the host writes them: a lower-case word ("added") is part of the id.</summary>
        public static string Base(string id)
        {
            var cut = id.LastIndexOf('_');
            return cut > 0 && cut < id.Length - 1 && id[(cut + 1)..].All(IsHostHexDigit) ? id[..cut] : id;
        }

        /// <summary>
        ///     The id a table knows the key by: the key itself when the table has it, the key without its suffix only when the
        ///     table has that one and not the whole; the key as it came when it has neither.
        /// </summary>
        public static string In(string id, Func<string, bool> known)
        {
            if (known(id))
            {
                return id;
            }

            var stripped = Base(id);
            return stripped.Length != id.Length && known(stripped) ? stripped : id;
        }

        private static bool IsHostHexDigit(char c) => c is (>= '0' and <= '9') or (>= 'A' and <= 'F');

        public static bool IsHiddenMonster(string id) => Base(id) == HiddenMonster;

        /// <summary>The game's flask-drain effects (BuffDefinitions: generic_flask_drain, monster_flask_drain_aura, flask_draining_lasso).</summary>
        public static bool IsFlaskDrain(string id) => id.Contains("flask_drain", StringComparison.Ordinal);
    }
}

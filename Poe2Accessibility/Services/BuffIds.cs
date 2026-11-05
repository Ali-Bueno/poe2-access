namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;

    /// <summary>Buff ids as GameHelper keys them: a definition id, sometimes with an instance suffix "_79". Pure.</summary>
    public static class BuffIds
    {
        /// <summary>The game's mark on a monster set aside: Count Geonor left behind while the Wolf fights on (read live, 2026-11-05).</summary>
        public const string HiddenMonster = "hidden_monster";

        /// <summary>The definition id without a trailing "_" and hex digits.</summary>
        public static string Base(string id)
        {
            var cut = id.LastIndexOf('_');
            return cut > 0 && cut < id.Length - 1 && id[(cut + 1)..].All(Uri.IsHexDigit) ? id[..cut] : id;
        }

        public static bool IsHiddenMonster(string id) => Base(id) == HiddenMonster;
    }
}

namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;

    /// <summary>Buff ids as GameHelper keys them: a definition id, sometimes with an instance suffix "_79". Pure.</summary>
    public static class BuffIds
    {
        /// <summary>The definition id without a trailing "_" and hex digits.</summary>
        public static string Base(string id)
        {
            var cut = id.LastIndexOf('_');
            return cut > 0 && cut < id.Length - 1 && id[(cut + 1)..].All(Uri.IsHexDigit) ? id[..cut] : id;
        }
    }
}

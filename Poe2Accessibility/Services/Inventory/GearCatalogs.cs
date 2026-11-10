namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;

    /// <summary>
    ///     An item base as the game's files give it: what it is, never what a single item of it holds (its modifiers,
    ///     rolled defences and requirements shown are the item's own and are read from the game).
    /// </summary>
    /// <param name="Id">The base's id in BaseItemTypes (<c>Metadata/Items/…</c>).</param>
    /// <param name="ItemClass">The item class id (helmet, body armour, spear, buckler…), as the files name it.</param>
    /// <param name="Names">Its name in each shipped language, by language code.</param>
    /// <param name="Armour">The base's armour before modifiers; 0 when it has none or is no armour piece.</param>
    /// <param name="Evasion">The base's evasion before modifiers; 0 when none.</param>
    /// <param name="EnergyShield">The base's energy shield before modifiers; 0 when none.</param>
    /// <param name="RequiredLevel">The level the base asks for; 0 when the files give none.</param>
    public sealed record ItemBase(
        string Id, string ItemClass, IReadOnlyDictionary<string, string> Names, int Armour, int Evasion, int EnergyShield, int RequiredLevel)
    {
        /// <summary>Its name in <paramref name="language"/>, else in English, else its id's last part.</summary>
        public string Name(string language) =>
            this.Names.TryGetValue(language, out var name) && name.Length > 0 ? name
            : this.Names.TryGetValue("en", out var english) && english.Length > 0 ? english
            : this.Id[(this.Id.LastIndexOf('/') + 1)..];
    }

    /// <summary>The item bases of the game's files.</summary>
    public interface IItemBases
    {
        /// <summary>
        ///     The base that carries <paramref name="name"/> in any language (case and spacing ignored); null when none
        ///     does or when bases of different item classes share it.
        /// </summary>
        ItemBase? ByName(string name);

        /// <summary>The base with this id (the whole path or its last part, case ignored); null when unknown.</summary>
        ItemBase? ById(string id);
    }

    /// <summary>A modifier line told apart from its numbers: which line of the game's stat texts it is, and the numbers in it.</summary>
    /// <param name="Template">The line's identity, the same whatever language it was written in.</param>
    /// <param name="Numbers">The numbers in it, in the order the template takes them.</param>
    public sealed record ModLine(string Template, IReadOnlyList<double> Numbers);

    /// <summary>The game's modifier texts in every shipped language, so a line written in one can be found and said in another.</summary>
    public interface IModTexts
    {
        /// <summary>
        ///     The modifier line <paramref name="text"/> is, in whichever language it is written ("+16% to Fire Resistance",
        ///     "+16% a la resistencia al fuego"); null when no line of the game's texts fits it, or more than one does.
        /// </summary>
        ModLine? Parse(string text);

        /// <summary>The line in <paramref name="language"/> with its numbers; empty when the template has no text there.</summary>
        string Say(ModLine line, string language);
    }
}

namespace Poe2Accessibility.Services
{
    /// <summary>The Ascendancy trials' icons: an affliction or boon on the Sekhemas HUD, a Trial of Chaos modifier, a class.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>What the trials' icons stand for (catalog/trials.json).</summary>
        public TrialCatalog Trials { get; set; } = TrialCatalog.Empty;

        /// <summary>An icon-only element of the trials named by its art, "Affliction: Honed Claws. Monsters deal 30% more Damage"; empty for any other.</summary>
        private string TrialIcon(in UiElementReader.ElementInfo info) =>
            this.elements.ReadTextureKey(info) is { } art && this.Trials.KeyOfIcon(art) is { } key
                ? Announcements.TrialEntry(TrialCatalog.KindOf(key), this.Trials.Text(key), this.Trials.About(key))
                : string.Empty;
    }
}

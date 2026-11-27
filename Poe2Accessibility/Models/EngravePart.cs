namespace Poe2Accessibility.Models
{
    /// <summary>What a text of the support engraving window is, for the narrator (UiSemantics.EngravePartOf).</summary>
    public enum EngravePart
    {
        /// <summary>Not the focused choice's description: the tab, the choices, the build plan's list, the buttons.</summary>
        Other,

        /// <summary>A line of the focused support's description, its "Supportable Skills" list included.</summary>
        Description,
    }
}

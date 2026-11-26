namespace Poe2Accessibility.Services
{
    /// <summary>
    ///     The scanner's ground past closed barriers: the entries for it, the gate's state near the guide, the boss on the
    ///     player's side, and the barriers found shut (docs/superpowers/specs/2026-11-26-ground-behind-closed-barriers-design.md).
    /// </summary>
    public sealed partial class EntityCatalog
    {
        /// <summary>Whether the ground past a closed barrier is listed among the unexplored (setting GroundBehindBarriers).</summary>
        public bool GroundBehindBarriers { get; set; } = true;
    }
}

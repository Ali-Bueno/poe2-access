namespace Poe2Accessibility
{
    using System.IO;
    using Poe2Accessibility.Services;

    /// <summary>Boss fights' turns, the way out of their danger areas and their mechanic lines (docs/research/boss-mechanics.md).</summary>
    public sealed partial class Poe2AccessibilityCore
    {
        /// <summary>Loads the boss catalog every boss feature reads and makes the watch the boss watch feeds.</summary>
        private BossMechanicsWatch BossMechanics(UiElementReader elements, PluginLog log)
        {
            var catalog = BossMechanicsCatalog.Load(Path.Join(this.DllDirectory, "catalog"));
            BossMechanicsCatalog.Shared = catalog;
            log.Info($"Boss mechanics: {catalog.Bosses} bosses, {catalog.Lines} mechanic lines, {catalog.MoveRules} movement rules, {catalog.Daemons} helpers, {catalog.Places} places");
            return new BossMechanicsWatch(this.output, elements, log)
            {
                Catalog = catalog,
                SayAdds = () => !this.Settings.AnnounceEnemies,
                Heartbeat = () => this.Settings.BossWeakPoint,
            };
        }
    }
}

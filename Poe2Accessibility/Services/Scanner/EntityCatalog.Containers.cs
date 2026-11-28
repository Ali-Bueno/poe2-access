namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteEnums;
    using GameReader.RemoteEnums.Entity;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>Strongbox logging and the essence monsters held by their crystals.</summary>
    public sealed partial class EntityCatalog
    {
        /// <summary>
        ///     What a strongbox reads as, each time the read changes: the host's mods components carry its rarity
        ///     (spoken since the first live box) and its modifiers; whether a spent currency shows in them — the
        ///     lists rebuild only when their component's address changes — and where corruption reads (the
        ///     <c>dropped_items_are_corrupted</c> stat among the mods' stats, or the entity's own) is what
        ///     speaking either waits on, so every change of the whole read is logged as a session's log confirms
        ///     it (docs/research/strongbox-currency.md §5). The change test is the read's shape (rarities, mod
        ///     counts, corruption values), compared without building the line; the line itself, with the mod
        ///     names in a fixed order, is built only when the shape changed.
        /// </summary>
        private void LogStrongbox(Entity entity, ObjectMagicProperties? properties)
        {
            var corruptByMods = properties != null && properties.ModStats.TryGetValue(GameStats.dropped_items_are_corrupted, out var byMods) ? byMods : 0;
            var mods = entity.TryGetComponent<Mods>(out var itemMods) ? itemMods : null;
            var stats = entity.TryGetComponent<Stats>(out var ownStats) ? ownStats : null;
            var corruptByItems = stats != null && stats.StatsChangedByItems.TryGetValue(GameStats.dropped_items_are_corrupted, out var byItems) ? byItems : 0;
            var corruptByBuffs = stats != null && stats.StatsChangedByBuffAndActions.TryGetValue(GameStats.dropped_items_are_corrupted, out var byBuffs) ? byBuffs : 0;
            var read = (HasProperties: properties != null, Rarity: properties != null ? properties.Rarity : default(Rarity), ModCount: properties?.ModNames.Count ?? 0, CorruptByMods: corruptByMods,
                HasMods: mods != null, ModsRarity: mods != null ? mods.Rarity : default(Rarity), Implicit: mods?.ImplicitMods.Count ?? 0, Explicit: mods?.ExplicitMods.Count ?? 0,
                HasStats: stats != null, CorruptByItems: corruptByItems, CorruptByBuffs: corruptByBuffs);
            if (this.strongboxReads.TryGetValue(entity.Id, out var last) && last == read)
            {
                return;
            }

            this.strongboxReads[entity.Id] = read;
            var objectMods = properties != null
                ? $"{properties.Rarity}, mods [{string.Join(", ", properties.ModNames.OrderBy(mod => mod, StringComparer.Ordinal))}], corrupt stat {corruptByMods}" : "none";
            var modsRead = mods != null ? $"{mods.Rarity}, {mods.ImplicitMods.Count} implicit, {mods.ExplicitMods.Count} explicit" : "none";
            var own = stats != null ? $"{corruptByItems}/{corruptByBuffs}" : "none";
            this.log.Info($"Strongbox mods: {entity.Path}: ObjectMagicProperties {objectMods}; Mods {modsRead}; Stats corrupt {own}");
        }

        /// <summary>
        ///     The essences a crystal holds by the game's names of them; none when they do not read or have no name in the
        ///     catalog. What was read is logged once per crystal: how the field is confirmed from a session's log.
        /// </summary>
        private IReadOnlyList<string> EssenceNamesOf(Entity crystal)
        {
            var ids = EssenceContents.Of(this.memory, this.elements, crystal);
            if (this.essenceRows.Add(crystal.Id))
            {
                this.log.Info($"Essence contents: {(ids.Count == 0 ? "unread" : string.Join(", ", ids))}");
            }

            var names = ids.Select(id => ItemNames.Of(id) ?? string.Empty).ToList();
            return names.Count > 0 && names.All(name => name.Length > 0) ? names : Array.Empty<string>();
        }

        /// <summary>A creature of the game's monster folder: what an essence's crystal holds.</summary>
        private static bool IsHeldMonster(Entity entity) =>
            entity.EntityType == EntityTypes.Monster && (entity.Path ?? string.Empty).StartsWith(CreatureFolder, StringComparison.Ordinal);

        /// <summary>The essence whose crystal stands where this monster is, as the scanner lists it; null when none holds it.</summary>
        private ScanEntry? EssenceHolding(Render monster)
        {
            var place = new Vector2(monster.WorldPosition.X, monster.WorldPosition.Y);
            foreach (var (_, entity) in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities)
            {
                if (entity.IsValid && entity.TryGetComponent<MinimapIcon>(out var icon) && icon.IconName == MinimapMarks.EssenceIcon &&
                    entity.TryGetComponent<Render>(out var render) &&
                    Vector2.Distance(place, new Vector2(render.WorldPosition.X, render.WorldPosition.Y)) < EssenceWorld &&
                    this.Classify(entity, render) is { } essence)
                {
                    return essence;
                }
            }

            return null;
        }

        /// <summary>The first name, in the client's language, of an awake entity within reach of a place that passes the test, in play or not.</summary>
        private string NameNear(Render thing, float reach, Func<Entity, bool> passes)
        {
            var place = new Vector2(thing.WorldPosition.X, thing.WorldPosition.Y);
            foreach (var (_, entity) in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities)
            {
                if (entity.IsValid && passes(entity) && entity.TryGetComponent<Render>(out var render) &&
                    Vector2.Distance(place, new Vector2(render.WorldPosition.X, render.WorldPosition.Y)) < reach &&
                    this.elements.ReadWStringStructAt(render.Address + GameLayout.RenderComponent.NameOffset) is { Length: > 0 } name)
                {
                    return name;
                }
            }

            return string.Empty;
        }
    }
}

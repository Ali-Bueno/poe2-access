namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    ///     Buffs that mean the player stands in harmful ground or an enemy's damaging zone (catalog/hazards.txt, from the
    ///     game's GroundEffectTypes and buff descriptions); an id that starts with one and "_" counts too
    ///     ("ground_living_blood_damage"). Pure apart from <see cref="Load"/>.
    /// </summary>
    public sealed class HazardGround
    {
        private readonly HashSet<string> ids;

        public HazardGround(IEnumerable<string> lines) =>
            this.ids = lines.Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#')).ToHashSet(StringComparer.Ordinal);

        public int Count => this.ids.Count;

        public static HazardGround Load(string path) => new(File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>());

        public bool IsHazard(string buffId)
        {
            var id = BuffIds.Base(buffId);
            for (var end = id.Length; end > 0; end = id.LastIndexOf('_', end - 1))
            {
                if (this.ids.Contains(id[..end]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

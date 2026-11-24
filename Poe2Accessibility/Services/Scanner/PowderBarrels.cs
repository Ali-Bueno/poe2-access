namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     The pirates' powder barrels of Journey's End: no name, no mark and nothing to target, they stand until a trail of
    ///     gunpowder lights them (`prime`: fuse and ignition sounds) and blow up (`explode`), by their scripts
    ///     (Metadata/Monsters/Pirates/objects/Barrel*.ot, docs/research/powder-barrels.md). Pure.
    /// </summary>
    public static class PowderBarrels
    {
        /// <summary>A barrel that has not been lit: its entry's kind and its sound.</summary>
        public const string Kind = "powder_barrel";

        /// <summary>A barrel about to blow up.</summary>
        public const string LitKind = "powder_fuse";

        private const string Folder = "Metadata/Monsters/Pirates/objects/";

        private const string LitState = "prime";
        private const string BlownState = "explode";

        /// <summary>The scripts with those two states; BarrelForPickup is a pirate's ammunition, with neither.</summary>
        private static readonly string[] Scripts = { "BarrelPlaced", "BarrelPlacedSecondary", "BarrelLandmark" };

        public static bool Is(string path) =>
            path.StartsWith(Folder, StringComparison.Ordinal) && Array.IndexOf(Scripts, path[Folder.Length..]) >= 0;

        /// <summary>Its kind by its states; null once it blew up.</summary>
        public static string? KindOf(IEnumerable<(string Name, long Value)> states)
        {
            var kind = Kind;
            foreach (var (name, value) in states)
            {
                if (value > 0 && name == BlownState)
                {
                    return null;
                }

                kind = value > 0 && name == LitState ? LitKind : kind;
            }

            return kind;
        }

        /// <summary>Told by its sound alone: a camp holds a dozen, each named on approach would drown the rest.</summary>
        public static bool IsSoundOnly(string kind) => kind is Kind or LitKind;
    }
}

namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>Controls nothing names: said by their picture's file name, and described in the log so the window can be fixed from it.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>The siblings of a control compared for its picture's stem; a longer list is a page of rows, not a widget's neighbours.</summary>
        private const int MaxSiblingsCompared = 24;

        private readonly HashSet<string> namedFromArt = new(StringComparer.Ordinal);

        /// <summary>Logs the controls named from their art (the log keeps what the player heard, to be fixed with a real name).</summary>
        public Action<string>? ArtLog { get; set; }

        /// <summary>The words of the picture of a control nothing else names, said as the picture's file name; empty when none gives words.</summary>
        private string ArtFileName(in UiElementReader.ElementInfo info)
        {
            var own = new List<string>();
            this.CollectArt(info, ArtSearchDepth, own);
            if (own.Count == 0 || ArtWords.Pick(own, this.SiblingArt(info)) is not { } name)
            {
                return string.Empty;
            }

            if (this.namedFromArt.Count > UnnamedFocus.MaxLogged)
            {
                this.namedFromArt.Clear();
            }

            if (this.namedFromArt.Add($"{info.Address.ToInt64():X}:{name.Stem}"))
            {
                this.ArtLog?.Invoke($"{UnnamedFocus.ArtPrefix} {name.Stem} -> {name.Words} (0x{info.Address.ToInt64():X})");
            }

            return Announcements.ControlFromArt(name.Words);
        }

        private List<IReadOnlyList<string>> SiblingArt(in UiElementReader.ElementInfo info)
        {
            var siblings = new List<IReadOnlyList<string>>();
            if (!this.elements.TryReadElement(info.Raw.ParentPtr, out var parent))
            {
                return siblings;
            }

            foreach (var child in this.elements.ReadChildren(parent))
            {
                if (child == info.Address || siblings.Count >= MaxSiblingsCompared || !this.elements.TryReadElement(child, out var sibling) || !sibling.IsVisibleFlag)
                {
                    continue;
                }

                var keys = new List<string>();
                this.CollectArt(sibling, ArtSearchDepth, keys);
                siblings.Add(keys);
            }

            return siblings;
        }

        /// <summary>A line to log for a focus read without a name: where it sits and which pictures it shows (see <see cref="UnnamedFocus"/>).</summary>
        public string UnnamedDetails(IntPtr address, UiRole role, IntPtr layer)
        {
            if (!this.elements.TryReadElement(address, out var info))
            {
                return string.Empty;
            }

            var art = new List<string>();
            this.CollectArt(info, ArtSearchDepth, art);
            var layerName = this.elements.TryReadElement(layer, out var layerInfo) ? layerInfo.Name : string.Empty;
            return UnnamedFocus.Details(
                role.ToString(),
                this.elements.ReadAncestors(info, UnnamedFocus.MaxAncestors).Select(ancestor => ancestor.Name).ToList(),
                art.Select(ArtWords.Stem).Distinct().ToList(),
                this.elements.ReadChildren(info).Length,
                layerName);
        }
    }
}

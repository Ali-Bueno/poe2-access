namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameHelper.Plugin;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     Markers on a map panel (the checkpoint travel map, 2026-10-23): sibling buttons without text, each named
    ///     only by a one-line tooltip — "Estás aquí" for the one the player stands at, "Viajar al punto de control" for
    ///     every other. The game names none of them, so each is told apart by where it lies from the player's own
    ///     marker: "Viajar al punto de control, arriba a la derecha, 2 de 4". The map is drawn the way the camera
    ///     shows the world, so screen directions are the stick directions.
    /// </summary>
    public sealed partial class UiSemantics
    {
        /// <summary>Fewer markers than this is not a map.</summary>
        private const int MinMapMarkers = 2;

        /// <summary>Markers closer than this (pixels) to the player's own are "here".</summary>
        private const float SameSpotPixels = 8f;

        /// <summary>Debug log for research (set by the plugin).</summary>
        public System.Action<string>? Trace { get; set; }

        /// <summary>The map whose markers were last read, and the marker it opened on.</summary>
        private (System.IntPtr Parent, System.IntPtr Marker) mapHere;

        private AccessibleUiNode? DescribeMapMarker(in UiElementReader.ElementInfo info)
        {
            if (!this.InGame || this.elements.TryReadText(info.Address, out var own) && own.Length > 0 ||
                !this.elements.TryReadElement(info.Raw.ParentPtr, out var parent))
            {
                return null;
            }

            var markers = new List<(System.IntPtr Address, string Label, Vector2 Centre)>();
            foreach (var siblingAddress in this.elements.ReadChildren(parent))
            {
                if (!this.elements.TryReadElement(siblingAddress, out var sibling) || !sibling.IsVisibleFlag ||
                    sibling.Raw.Flags != info.Raw.Flags || this.elements.TryReadText(siblingAddress, out var text) && text.Length > 0)
                {
                    continue;
                }

                var tooltip = this.TooltipLines(sibling);
                if (tooltip.Count != 1 || !PluginUiElementReflection.TryGetAbsoluteRect(siblingAddress, out var position, out var size))
                {
                    return null; // a marker is named by exactly one tooltip line
                }

                markers.Add((siblingAddress, tooltip[0], position + (size / 2)));
            }

            var focusAddress = info.Address;
            var self = markers.FindIndex(marker => marker.Address == focusAddress);
            if (markers.Count < MinMapMarkers || self < 0)
            {
                return null;
            }

            // The player's own marker is the one whose name no other marker shares; with only two markers both
            // are unique, and the one the map opened on (the game focuses the player's own) is used.
            if (this.mapHere.Parent != parent.Address)
            {
                this.mapHere = (parent.Address, focusAddress);
            }

            var unique = markers.GroupBy(marker => marker.Label).Where(group => group.Count() == 1).ToList();
            var hereIndex = unique.Count == 1
                ? markers.FindIndex(marker => marker.Label == unique[0].Key)
                : markers.FindIndex(marker => marker.Address == this.mapHere.Marker);
            var label = markers[self].Label;
            this.Trace?.Invoke($"Map markers: {markers.Count}, self {self}, here {hereIndex}, centres {string.Join(" ", markers.Select(marker => $"({marker.Centre.X:0},{marker.Centre.Y:0})"))}");
            if (hereIndex >= 0 && hereIndex != self)
            {
                var offset = markers[self].Centre - markers[hereIndex].Centre;
                if (offset.Length() >= SameSpotPixels)
                {
                    // Screen y grows downwards; StickDirections count counter-clockwise from right.
                    var angle = System.Math.Atan2(-offset.Y, offset.X) * 180 / System.Math.PI;
                    var octant = (int)(((System.Math.Round(angle / 45) % 8) + 8) % 8);
                    label = $"{label}, {Announcements.StickDirections[octant]}";
                }
            }

            return new AccessibleUiNode
            {
                Address = info.Address,
                Name = info.Name,
                Role = UiRole.Button,
                Text = label,
                IsVisible = true,
                Position = Announcements.ListPosition(self + 1, markers.Count),
                ListItems = markers.Select(marker => marker.Label).Distinct().ToArray(),
            };
        }
    }
}

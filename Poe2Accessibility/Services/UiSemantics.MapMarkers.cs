namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     Markers on a map panel (the checkpoint travel map, 2026-10-23): sibling buttons without text, each named
    ///     only by a one-line tooltip — "Estás aquí" for the one the player stands at, "Viajar al punto de control" for
    ///     every other. The game names none of them, so each is told apart by the named place beside it
    ///     (<see cref="MarkerPlaces"/>), where it lies from the player's own marker and how far: "Viajar al punto de
    ///     control, cerca de Sierraclara, arriba a la derecha, a 40 pasos, 2 de 4". The map is drawn the way the camera
    ///     shows the world, so screen directions are the stick directions.
    /// </summary>
    public sealed partial class UiSemantics
    {
        /// <summary>Fewer markers than this is not a map.</summary>
        private const int MinMapMarkers = 2;

        /// <summary>Debug log for research (set by the plugin).</summary>
        public System.Action<string>? Trace { get; set; }

        /// <summary>Per map marker (offset from the player's own, its index) the place beside it and the steps to it (set by the plugin).</summary>
        public System.Func<IReadOnlyList<Vector2>, int, IReadOnlyList<(string? Near, int Steps)?>>? MarkerPlaces { get; set; }

        /// <summary>The map whose markers were last read, and the marker it opened on.</summary>
        private (System.IntPtr Parent, System.IntPtr Marker) mapHere;

        /// <summary>Hops from the depth toggle up to the element holding the map widget ("WorldScreen", 4 measured).</summary>
        private const int MapWidgetHops = 6;

        /// <summary>The last button checked for being the depth toggle, and its map canvas (zero: not the toggle).</summary>
        private (System.IntPtr Button, System.IntPtr Canvas) depthToggle;

        /// <summary>
        ///     The quest map's surface/underground toggle says what the map shows ("debajo del suelo"); empty for any other
        ///     button. Its own memory does not change with a press: the map canvas holds it (GameLayout.WorldMap).
        /// </summary>
        private string MapDepthValue(in UiElementReader.ElementInfo info)
        {
            if (this.depthToggle.Button != info.Address)
            {
                this.depthToggle = (info.Address, this.elements.HasSound(info.Address, GameLayout.WorldMap.DepthToggleSound) ? this.MapCanvasNear(info) : System.IntPtr.Zero);
            }

            return this.depthToggle.Canvas == System.IntPtr.Zero ? string.Empty : this.elements.ReadSurfaceShown(this.depthToggle.Canvas) switch
            {
                true => Announcements.AboveGround,
                false => Announcements.BelowGround,
                null => string.Empty,
            };
        }

        /// <summary>The canvas of the map widget beside the toggle: the first child of "map_control" under one of its ancestors.</summary>
        private System.IntPtr MapCanvasNear(in UiElementReader.ElementInfo toggle)
        {
            foreach (var ancestor in this.elements.ReadAncestors(toggle, MapWidgetHops))
            {
                foreach (var child in this.elements.ReadChildren(ancestor))
                {
                    if (this.elements.TryReadElement(child, out var widget) && widget.Name == GameLayout.WorldMap.ControlName)
                    {
                        return this.elements.ReadChildren(widget).FirstOrDefault();
                    }
                }
            }

            return System.IntPtr.Zero;
        }

        private AccessibleUiNode? DescribeMapMarker(in UiElementReader.ElementInfo info)
        {
            if (!this.InGame || this.elements.TryReadText(info.Address, out var own) && own.Length > 0 ||
                !this.elements.TryReadElement(info.Raw.ParentPtr, out var parent))
            {
                return null;
            }

            // Markers are siblings: their centres in the map's own units, the same at any window size or zoom level.
            var markers = new List<(System.IntPtr Address, string Label, Vector2 Centre, float Width)>();
            foreach (var siblingAddress in this.elements.ReadChildren(parent))
            {
                if (!this.elements.TryReadElement(siblingAddress, out var sibling) || !sibling.IsVisibleFlag ||
                    sibling.Raw.Flags != info.Raw.Flags || this.elements.TryReadText(siblingAddress, out var text) && text.Length > 0)
                {
                    continue;
                }

                var tooltip = this.TooltipLines(sibling);
                if (tooltip.Count != 1)
                {
                    return null; // a marker is named by exactly one tooltip line
                }

                var (corner, size) = (sibling.Raw.RelativePosition, sibling.Raw.UnscaledSize);
                markers.Add((siblingAddress, tooltip[0], new Vector2(corner.X + (size.X / 2), corner.Y + (size.Y / 2)), size.X));
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
            var place = hereIndex >= 0 && this.MarkerPlaces?.Invoke(markers.Select(marker => marker.Centre - markers[hereIndex].Centre).ToList(), hereIndex) is { } places &&
                        self < places.Count ? places[self] : null;
            if (place?.Near is { Length: > 0 } near)
            {
                label = $"{label}, {Announcements.NearPlace(near)}";
            }

            if (hereIndex >= 0 && hereIndex != self)
            {
                var offset = markers[self].Centre - markers[hereIndex].Centre;
                if (offset.Length() >= markers[hereIndex].Width / 2)
                {
                    // Screen y grows downwards; StickDirections count counter-clockwise from right.
                    label = $"{label}, {Announcements.StickDirections[PassiveStick.Nearest(offset)]}";
                }
            }

            if (place?.Steps is > 0 and var steps)
            {
                label = $"{label}, {Announcements.StepsAway(steps)}";
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

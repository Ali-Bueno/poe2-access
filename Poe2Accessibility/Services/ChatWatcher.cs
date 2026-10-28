namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    /// <summary>
    ///     New chat lines worth hearing: NPC shouts ("Wounded Man: Reach... Clearfell..."), the character's own
    ///     remarks, system notices, whispers and party. Global ('#') and trade ('$') channels are other players'
    ///     chatter, said only when the player asks for them; the same line twice within a few seconds is said once. The chat layer's message list is its descendant
    ///     with the most text lines (a list of 74 textless icons beside it took the place of a short history after a fresh
    ///     start and nothing was read all evening, 2026-10-27); lines present when the watcher first sees it are history,
    ///     not news, unless the chat was seen empty first, and the game rebuilds the line elements, so new lines come
    ///     from the text sequence (<see cref="ChatDiff"/>). A new
    ///     channel chosen in the panel's channel list is said once the list closes on it.
    /// </summary>
    public sealed class ChatWatcher
    {
        /// <summary>Chat layer → frame → scroll → list: the measured list sits three levels down.</summary>
        private const int ListSearchDepth = 4;

        private const int PollMs = 250;

        /// <summary>A line repeated within this time is not said again (AFK on/off while idling, repeated shouts).</summary>
        private const int RepeatWindowMs = 5000;

        /// <summary>Lines in a list first found after the chat was seen empty that are still news, not history.</summary>
        private const int FreshLines = 5;

        private readonly UiElementReader elements;
        private readonly ComboBoxReader comboBoxes;
        private readonly Action<string> speak;
        private readonly Stopwatch sincePoll = Stopwatch.StartNew();
        private readonly Dictionary<string, long> spokenAt = new();
        private List<string> lines = new();
        private IntPtr layer;
        private IntPtr list;
        private bool seenEmpty;
        private bool? panelShown;
        private IntPtr channelList;
        private string channel = string.Empty;

        public ChatWatcher(UiElementReader elements, ComboBoxReader comboBoxes, Action<string> speak)
        {
            this.elements = elements;
            this.comboBoxes = comboBoxes;
            this.speak = speak;
        }

        /// <summary>
        ///     The chat panel shown or hidden (LT in a panel): said with its hint, which names the button that types
        ///     ("Chat. Mantén presionado LT para chatear", 2026-10-24); null keeps it silent.
        /// </summary>
        public Action<string>? PanelChanged { get; set; }

        /// <summary>Another channel chosen for what the player types ("Channel: Trade"); null keeps it silent.</summary>
        public Action<string>? ChannelChanged { get; set; }

        /// <summary>Whether global and trade lines are said too (a setting).</summary>
        public Func<bool>? ReadPlayerChannels { get; set; }

        /// <summary>Per frame with the current chat layer (zero outside the game).</summary>
        public void Tick(IntPtr chatLayer)
        {
            if (chatLayer != this.layer)
            {
                this.layer = chatLayer;
                this.list = IntPtr.Zero;
                this.seenEmpty = false;
                this.lines.Clear();
                this.panelShown = null;
                this.channelList = IntPtr.Zero;
                this.channel = string.Empty;
            }

            if (this.layer == IntPtr.Zero || this.sincePoll.ElapsedMilliseconds < PollMs)
            {
                return;
            }

            this.sincePoll.Restart();
            this.TrackPanel();
            this.TrackChannel();
            var baseline = this.list == IntPtr.Zero;
            if (baseline && !this.TryFindList())
            {
                this.seenEmpty = true;
                return;
            }

            if (!this.elements.TryReadElement(this.list, out var listInfo))
            {
                this.list = IntPtr.Zero;
                return;
            }

            var current = new List<string>();
            foreach (var line in this.elements.ReadChildren(listInfo))
            {
                if (this.elements.TryReadText(line, out var text) && text.Length > 0)
                {
                    current.Add(text);
                }
            }

            var added = !baseline ? ChatDiff.Appended(this.lines, current)
                : this.seenEmpty && current.Count <= FreshLines ? current
                : Array.Empty<string>();
            this.seenEmpty = false;
            this.lines = current;
            foreach (var text in added)
            {
                var now = Environment.TickCount64;
                if ((!ChatDiff.IsPlayerChannel(text) || this.ReadPlayerChannels?.Invoke() == true) &&
                    !(this.spokenAt.TryGetValue(text, out var at) && now - at < RepeatWindowMs))
                {
                    this.spokenAt[text] = now;
                    this.speak(ChatDiff.Spoken(text));
                }
            }
        }

        private void TrackPanel()
        {
            var hint = this.PanelHint();
            if (this.panelShown is { } was && was != (hint != null))
            {
                this.PanelChanged?.Invoke(hint != null ? Announcements.ChatShown(hint) : Announcements.ChatHidden);
            }

            this.panelShown = hint != null;
        }

        /// <summary>The channel list is a direct child of the layer; its committed entry is the channel (Local, Global, Party…).</summary>
        private void TrackChannel()
        {
            if (this.channelList == IntPtr.Zero)
            {
                this.channelList = this.FindChannelList();
                if (this.channelList == IntPtr.Zero)
                {
                    return;
                }
            }

            var combo = this.comboBoxes.Read(this.channelList);
            if (combo.Count == 0)
            {
                this.channelList = IntPtr.Zero;
                return;
            }

            if (combo.Selected.Length > 0 && combo.Selected != this.channel)
            {
                if (this.channel.Length > 0)
                {
                    this.ChannelChanged?.Invoke(Announcements.ChatChannel(combo.Selected));
                }

                this.channel = combo.Selected;
            }
        }

        private IntPtr FindChannelList()
        {
            if (this.elements.TryReadElement(this.layer, out var root))
            {
                foreach (var child in this.elements.ReadChildren(root))
                {
                    if (this.elements.TryReadElement(child, out var info) && this.elements.HasComboBoxParts(info))
                    {
                        return child;
                    }
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        ///     The shown panel's hint: a visible direct child of the visible layer with a button glyph. Lines that flash
        ///     up in play show the layer without it.
        /// </summary>
        private string? PanelHint()
        {
            if (!this.elements.TryReadElement(this.layer, out var root) || !root.IsVisibleFlag)
            {
                return null;
            }

            foreach (var child in this.elements.ReadChildren(root))
            {
                if (this.elements.TryReadElement(child, out var info) && info.IsVisibleFlag &&
                    this.elements.TryReadText(child, out var text, out var tagged) && text.Length > 0 && ControllerGlyphs.HasGlyphs(tagged))
                {
                    return ControllerGlyphs.Render(tagged);
                }
            }

            return null;
        }

        private bool TryFindList()
        {
            if (!this.elements.TryReadElement(this.layer, out var root))
            {
                return false;
            }

            var best = (Address: IntPtr.Zero, Children: 0);
            var queue = new Queue<(UiElementReader.ElementInfo Node, int Depth)>();
            queue.Enqueue((root, 0));
            while (queue.Count > 0)
            {
                var (node, depth) = queue.Dequeue();
                var children = this.elements.ReadChildren(node);
                if (children.Length > best.Children && this.elements.TryReadText(children[^1], out var newest) && newest.Length > 0)
                {
                    best = (node.Address, children.Length);
                }

                if (depth == ListSearchDepth || children.Length > 32)
                {
                    continue; // the list itself: its lines hold no further lists
                }

                foreach (var child in children)
                {
                    if (this.elements.TryReadElement(child, out var info))
                    {
                        queue.Enqueue((info, depth + 1));
                    }
                }
            }

            this.list = best.Address;
            return this.list != IntPtr.Zero;
        }
    }
}

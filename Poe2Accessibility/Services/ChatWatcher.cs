namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    /// <summary>
    ///     New chat lines worth hearing: NPC shouts ("Wounded Man: Reach... Clearfell..."), the character's own
    ///     remarks, system notices, whispers and party. Global ('#') and trade ('$') channels are other players'
    ///     chatter and stay silent; the same line twice within a few seconds is said once. The chat layer's message list is its descendant
    ///     with the most children; lines present when the watcher first sees it are history, not news, and the
    ///     game rebuilds the line elements, so new lines come from the text sequence (<see cref="ChatDiff"/>).
    /// </summary>
    public sealed class ChatWatcher
    {
        /// <summary>Chat layer → frame → scroll → list: the measured list sits three levels down.</summary>
        private const int ListSearchDepth = 4;

        private const int PollMs = 250;

        /// <summary>A line repeated within this time is not said again (AFK on/off while idling, repeated shouts).</summary>
        private const int RepeatWindowMs = 5000;

        private static readonly char[] PlayerChannels = { '#', '$' };

        private readonly UiElementReader elements;
        private readonly Action<string> speak;
        private readonly Stopwatch sincePoll = Stopwatch.StartNew();
        private readonly Dictionary<string, long> spokenAt = new();
        private List<string> lines = new();
        private IntPtr layer;
        private IntPtr list;

        public ChatWatcher(UiElementReader elements, Action<string> speak)
        {
            this.elements = elements;
            this.speak = speak;
        }

        /// <summary>Per frame with the current chat layer (zero outside the game).</summary>
        public void Tick(IntPtr chatLayer)
        {
            if (chatLayer != this.layer)
            {
                this.layer = chatLayer;
                this.list = IntPtr.Zero;
                this.lines.Clear();
            }

            if (this.layer == IntPtr.Zero || this.sincePoll.ElapsedMilliseconds < PollMs)
            {
                return;
            }

            this.sincePoll.Restart();
            var baseline = this.list == IntPtr.Zero;
            if (baseline && !this.TryFindList())
            {
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

            var added = baseline ? Array.Empty<string>() : ChatDiff.Appended(this.lines, current);
            this.lines = current;
            foreach (var text in added)
            {
                var now = Environment.TickCount64;
                if (Array.IndexOf(PlayerChannels, text[0]) < 0 &&
                    !(this.spokenAt.TryGetValue(text, out var at) && now - at < RepeatWindowMs))
                {
                    this.spokenAt[text] = now;
                    this.speak(text);
                }
            }
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
                if (children.Length > best.Children)
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

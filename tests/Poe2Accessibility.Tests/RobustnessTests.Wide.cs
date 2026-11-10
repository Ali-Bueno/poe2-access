namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>
    ///     The searches of a whole object, for a field moved farther than a class grows: each finds what the near search
    ///     misses, and refuses a place that holds one of its two facts or shares the proof with another place.
    /// </summary>
    public static partial class RobustnessTests
    {
        private const int Radius = 0x100;
        private const int Step = 8;
        private const int ElementBytes = 0x800;

        // An MSVC wide string as the element holds it: 16 bytes of text or pointer, the length, the capacity (7 while inline).
        private const int WStringBytes = 0x20;
        private const int InlineCapacity = 7;

        private static IEnumerable<(string Name, Action Test)> Wide() => new (string, Action)[]
        {
            (nameof(AWholeObjectGivesOnlyASingleAnswer), AWholeObjectGivesOnlyASingleAnswer),
            (nameof(AControlNameMovedFarNeedsTwoKnownNames), AControlNameMovedFarNeedsTwoKnownNames),
            (nameof(LabelTextsMovedFarNeedTheirThreeFieldsAtOneShift), LabelTextsMovedFarNeedTheirThreeFieldsAtOneShift),
            (nameof(ATextureMovedFarNeedsItsArtAndASilentNeighbour), ATextureMovedFarNeedsItsArtAndASilentNeighbour),
            (nameof(ControlFieldsMovedFarWaitForASecondControl), ControlFieldsMovedFarWaitForASecondControl),
            (nameof(ALayerIsFoundByItsElementThenItsSlot), ALayerIsFoundByItsElementThenItsSlot),
            (nameof(TheGraphFileMovedFarMustLeadToEdges), TheGraphFileMovedFarMustLeadToEdges),
            (nameof(TheAllocatedListFollowsTheFileOrIsFoundAnywhere), TheAllocatedListFollowsTheFileOrIsFoundAnywhere),
            (nameof(TheEdgeListIsFoundAnywhereInTheFile), TheEdgeListIsFoundAnywhereInTheFile),
            (nameof(AKeyMovedFarIsFoundWhenTheStructHoldsOneRow), AKeyMovedFarIsFoundWhenTheStructHoldsOneRow),
            (nameof(TheStunRecordMovedFarNeedsOneSharedPlace), TheStunRecordMovedFarNeedsOneSharedPlace),
        };

        private static void AWholeObjectGivesOnlyASingleAnswer()
        {
            Equal((int?)0x400, OffsetSearch.Only(0x98, 8, 0x800, 8, at => at == 0x400), "the one place that fits, however far");
            Equal((int?)null, OffsetSearch.Only(0x98, 8, 0x800, 8, at => at is 0x400 or 0x600), "two places: not chosen between");
            Equal((int?)null, OffsetSearch.Only(0x98, 8, 0x800, 8, _ => false), "none");
            Equal((int?)0x400, OffsetSearch.Only(0x98, 8, 0x800, 8, at => at is 0x400 or 0x600, _ => 7), "two slots of one value: the nearest");
            Equal((int?)null, OffsetSearch.Only(0x98, 8, 0x800, 8, at => at is 0x400 or 0x600, at => at), "two values: none");
            Equal((int?)null, OffsetSearch.Only(0x98, 8, 0x800, 8, at => at == 0x404), "only places in step with the measured one are looked at");
            Equal((int?)-0x300, OffsetSearch.Only(0, -0x360, 0x2A8, 8, shift => shift == -0x300), "a shift may be negative");
            Equal((int?)0x98, OffsetSearch.NearThenWide(0x98, 0x100, 8, at => at is 0x98 or 0x400, 8, 0x800, _ => false), "near first: a healthy field never reaches the wide search");
        }

        private static void AControlNameMovedFarNeedsTwoKnownNames()
        {
            const int measured = 0x98;
            var known = new[] { "root", "HUD", "map" };
            byte[] Node(params (int At, string Name)[] names)
            {
                var block = new byte[ElementBytes];
                foreach (var (at, name) in names)
                {
                    PutWString(block, at, name);
                }

                return block;
            }

            int? Find(params byte[][] nodes) => ElementFieldSearch.Name(nodes, InlineWString, known, measured, Radius, Step);
            Equal((int?)measured, Find(Node((measured, "HUD")), Node()), "where measured, one name is enough");
            Equal((int?)(measured + 0x18), Find(Node((measured + 0x18, "root")), Node()), "moved a little: the nearest");

            const int far = measured + 0x300;
            Equal((int?)null, OffsetSearch.First(measured, Radius, Step, at => ElementFieldSearch.KnownNames(new[] { Node((far, "HUD")), Node((far, "map")) }, at, InlineWString, known) >= 1, Step), "the near search gives up");
            Equal((int?)far, Find(Node((far, "HUD")), Node((far, "map")), Node((far, "Other"))), "the whole element: two elements, two known names");
            Equal((int?)null, Find(Node((far, "HUD")), Node((far, "Other"))), "one known name far away is a coincidence");
            Equal((int?)null, Find(Node((far, "HUD")), Node((far, "HUD"))), "the same name twice is one fact, not two");
            Equal((int?)far, Find(Node((far, "HUD"), (far + 0x80, "map")), Node((far, "map"))), "a lone known name elsewhere does not disturb it");
            Equal((int?)null, Find(Node((far, "HUD"), (far + 0x80, "HUD")), Node((far, "map"), (far + 0x80, "map"))), "two places with the full proof: none");
        }

        private static void LabelTextsMovedFarNeedTheirThreeFieldsAtOneShift()
        {
            const int tagged = 0x360, text = 0x490, copy = 0x538, hits = 5;
            static bool TextAt(byte[] block, int shift) =>
                InlineWString(block, tagged + shift) != null && InlineWString(block, text + shift) is { Length: > 0 } read && read == InlineWString(block, copy + shift);
            static byte[] Label(int shift, string words, string? copied = null)
            {
                var block = new byte[ElementBytes];
                PutWString(block, tagged + shift, words);
                PutWString(block, text + shift, words);
                PutWString(block, copy + shift, copied ?? words);
                return block;
            }

            int? Find(IEnumerable<byte[]> labels) => ElementFieldSearch.TextShift(labels.ToList(), TextAt, Radius, Step, -tagged, ElementBytes - copy - WStringBytes + Step, hits);
            var words = new[] { "Play", "Options", "Exit", "Back", "Accept", "Cancel" };
            Equal((int?)0, Find(words.Select(word => Label(0, word))), "where measured");
            Equal((int?)0x10, Find(words.Select(word => Label(0x10, word))), "moved a little");
            Equal((int?)0x200, Find(words.Select(word => Label(0x200, word))), "the base control grew by 0x200: the near search fails, the whole element finds it");
            Equal((int?)-0x180, Find(words.Select(word => Label(-0x180, word))), "or shrank");
            Equal((int?)null, Find(words.Select(word => Label(0x200, word, "Other"))), "a text whose copy differs is not a label's text");
            Equal((int?)null, Find(words.Take(hits - 1).Select(word => Label(0x200, word))), "four labels are too few");
            Equal((int?)null, Find(words.Select(word => Label(0x200, word)).Concat(words.Select(word => Label(-0x180, word)))), "two shifts that both read: none");
        }

        private static void ATextureMovedFarNeedsItsArtAndASilentNeighbour()
        {
            const int measured = 0x240, hits = 5;
            const long art = 0x7000_0000, other = 0x6000_0000;
            static bool LeadsToArt(long pointer) => pointer >= art;
            static byte[] Image(int at, long texture, long next = 0)
            {
                var block = new byte[ElementBytes];
                BitConverter.TryWriteBytes(block.AsSpan(at), texture);
                BitConverter.TryWriteBytes(block.AsSpan(at + Step), next);
                return block;
            }

            int? Find(IEnumerable<byte[]> images) => ElementFieldSearch.Texture(images.ToList(), LeadsToArt, measured, Radius, Step, hits);
            var six = Enumerable.Range(0, 6).ToList();
            Equal((int?)measured, Find(six.Select(index => Image(measured, art + index))), "where measured");
            Equal((int?)(measured + 0x300), Find(six.Select(index => Image(measured + 0x300, art + index))), "moved beyond the near search: found in the whole element");
            Equal((int?)null, Find(six.Select(index => Image(measured + 0x300, other + index))), "pointers that lead to no art");
            Equal((int?)null, Find(six.Take(4).Select(index => Image(measured + 0x300, art + index))), "four images are too few");

            // Both image classes side by side: the smaller one's pointer sits one step before the larger's, and only the larger's
            // place has a silent neighbour after it.
            Equal((int?)(measured + 0x308), Find(six.Select(index => Image(measured + 0x300, art + index, art + 0x100 + index))), "of two adjacent pointers to art, the later");
        }

        private static void ControlFieldsMovedFarWaitForASecondControl()
        {
            var log = new PluginLog(System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests"), false);
            static bool Far(int shift) => shift == 0x300;
            var controls = new ControlFields(new CompatibilityReport(log), () => 0);
            Equal(0, controls.Shift(ControlGroup.Slider, 1, Far), "nothing nearby on the first control: read where measured");
            Equal(0, controls.Shift(ControlGroup.Slider, 2, Far), "nor on the second");
            Equal(0, controls.Shift(ControlGroup.Slider, 3, Far), "the third miss searches the whole control, and only notes what it finds");
            Equal(0, controls.Current(ControlGroup.Slider), "a far place found on one control is not read yet");
            Equal(0, controls.Shift(ControlGroup.Slider, 3, Far), "the same control proves nothing more");
            Equal(0x300, controls.Shift(ControlGroup.Slider, 4, Far), "a second control fits there too: taken");
            Equal(true, controls.IsKnown(ControlGroup.Slider), "and kept");

            static bool Twice(int shift) => shift is 0x300 or 0x400;
            var unsure = new ControlFields(new CompatibilityReport(log), () => 0);
            foreach (var control in new[] { 1, 2, 3, 4, 5 })
            {
                Equal(0, unsure.Shift(ControlGroup.Slider, control, Twice), "two far places fit: not chosen between");
            }

            Equal(false, unsure.IsKnown(ControlGroup.Slider), "never taken");

            var alone = new ControlFields(new CompatibilityReport(log), () => 0);
            foreach (var control in new[] { 1, 2, 3 })
            {
                alone.Shift(ControlGroup.Slider, control, Far);
            }

            Equal(0, alone.Shift(ControlGroup.Slider, 4, _ => false), "the far place fits one control and not the next: a coincidence");
            Equal(false, alone.IsKnown(ControlGroup.Slider), "not taken");
        }

        private static void ALayerIsFoundByItsElementThenItsSlot()
        {
            const long hud = 0x5000, chat = 0x6000, stray = 0x7000;
            var slots = new long[0x400];
            slots[0x1558 / 8] = hud;
            slots[0xA60 / 8] = chat;
            var children = new HashSet<long> { hud };
            Equal((int?)0x1558, LayerSlots.Find(slots, children, 0x558), "the HUD's slot 0x1000 from where it was measured");
            Equal((int?)null, LayerSlots.Find(slots, new HashSet<long> { stray }, 0x558), "an element the tree shows and no slot holds: lost");
            Equal((int?)null, LayerSlots.Find(slots, new HashSet<long>(), 0x558), "no such element in the tree: a slot's content alone is never taken");
            slots[0x200 / 8] = hud;
            Equal((int?)0x200, LayerSlots.Find(slots, children, 0x558), "two slots of the same element: the nearest");
            Equal((int?)null, LayerSlots.Find(slots, new HashSet<long> { hud, chat }, 0x558), "slots of two elements that both carry the mark: not chosen between");
        }

        private static void TheGraphFileMovedFarMustLeadToEdges()
        {
            const int measured = 0x160, limit = 0x800;
            const long graph = 0x9000, copy = 0x9100, other = 0x9200;
            var named = new HashSet<long> { graph, copy };
            int? Find(Dictionary<int, long> state, params long[] withEdges) => PassiveGraphSearch.File(
                at => state.GetValueOrDefault(at), named.Contains, file => withEdges.Contains(file), measured, Radius, Step, limit);
            Equal((int?)measured, Find(new() { [measured] = graph }), "where measured the name is enough");
            Equal((int?)(measured + 8), Find(new() { [measured + 8] = graph }), "moved a little");
            Equal((int?)0x560, Find(new() { [0x560] = graph }, graph), "moved 0x400: named and leading to an edge list");
            Equal((int?)null, Find(new() { [0x560] = graph }), "named, but it leads to no edges: one fact");
            Equal((int?)null, Find(new() { [0x560] = other }, other), "edges, but not the graph's name");
            Equal((int?)0x560, Find(new() { [0x560] = graph, [0x700] = graph }, graph), "two slots of the same file");
            Equal((int?)null, Find(new() { [0x560] = graph, [0x700] = copy }, graph, copy), "two files that both pass: none");
        }

        private static void TheAllocatedListFollowsTheFileOrIsFoundAnywhere()
        {
            const int measured = 0x1A8, limit = 0x800;
            var graph = Enumerable.Range(100, 400).ToHashSet();
            var taken = new[] { 100, 101, 250 };
            int? Find(Dictionary<int, int[]> lists, int together = measured) => PassiveGraphSearch.Allocated(
                at => lists.GetValueOrDefault(at), at => lists.TryGetValue(at, out var list) ? list.Sum() + at : 0, graph, measured, together, Radius, Step, limit);
            Equal((int?)(measured + 0x20), Find(new() { [measured + 0x20] = taken }), "nearby");
            Equal((int?)0x5A8, Find(new() { [0x5A8] = taken }), "moved 0x400: found in the whole state");
            Equal((int?)null, Find(new() { [0x5A8] = new[] { 100, 101, 9000 } }), "a list with an unknown id never takes the place");
            Equal((int?)null, Find(new() { [0x5A8] = taken, [0x700] = new[] { 300, 301, 302 } }), "two lists of known ids far away: none");

            // The file beside it moved 0x38: that place is tried before the nearer list.
            Equal((int?)(measured + 0x38), Find(new() { [measured + 8] = new[] { 300, 301, 302 }, [measured + 0x38] = taken }, measured + 0x38), "as far as the file moved first");
        }

        private static void TheEdgeListIsFoundAnywhereInTheFile()
        {
            var measured = (Content: 0x28, List: 0x58);
            var limit = (0x200, 0x400);
            (int, int)? Find(params (int Content, int List, long Edges)[] lists) => PassiveGraphSearch.Edges(
                (content, list) => lists.FirstOrDefault(found => found.Content == content && found.List == list).Edges, measured, Radius, Step, limit);
            Equal(((int, int)?)(0x28, 0x58), Find((0x28, 0x58, 0x4000)), "where measured");
            Equal(((int, int)?)(0x30, 0x60), Find((0x30, 0x60, 0x4000)), "both moved a little");
            Equal(((int, int)?)(0x28, 0x300), Find((0x28, 0x300, 0x4000)), "the list moved far inside the content");
            Equal(((int, int)?)(0x180, 0x58), Find((0x180, 0x58, 0x4000)), "the content pointer moved far inside the file");
            Equal(((int, int)?)null, Find((0x180, 0x300, 0x4000), (0x1C0, 0x300, 0x5000)), "two different lists far away: none");
            Equal(((int, int)?)(0x180, 0x300), Find((0x180, 0x300, 0x4000), (0x1C0, 0x300, 0x4000)), "two ways to the same list: the nearest");
            Equal(((int, int)?)null, Find(), "no list reads as the graph's");
        }

        private static void AKeyMovedFarIsFoundWhenTheStructHoldsOneRow()
        {
            const int measured = 0x2D0;
            Equal((int?)null, DatKeySearch.Site(measured, at => at == 0x4D0), "beyond the near reach");
            Equal((int?)0x4D0, DatKeySearch.SiteWide(measured, at => at == 0x4D0 ? 0x6000 : 0), "one key of the table in the whole struct");
            Equal((int?)null, DatKeySearch.SiteWide(measured, at => at == 0x4D0 ? 0x6000 : at == 0x4F0 ? 0x6100 : 0), "keys to two rows (a canvas's hovered and current pin): none");
            Equal((int?)0x4D0, DatKeySearch.SiteWide(measured, at => at is 0x4D0 or 0x580 ? 0x6000 : 0), "two keys to the same row: the nearest");
            Equal((int?)null, DatKeySearch.SiteWide(measured, at => at == 0x5A0 ? 0x6000 : 0), "beyond twice where it was measured");
            Equal((int?)0x1F8, DatKeySearch.SiteWide(0, at => at == 0x1F8 ? 0x6000 : 0), "a key measured at the struct's start is looked for through its first 0x200 bytes");
        }

        private static void TheStunRecordMovedFarNeedsOneSharedPlace()
        {
            var five = Enumerable.Repeat<IReadOnlyList<int>>(new[] { 0x9D8 }, 5).ToList();
            Equal((int?)0x9D8, PoiseSearch.AgreedOnly(five), "five actors share one place");
            Equal((int?)null, PoiseSearch.AgreedOnly(five.Take(4).ToList()), "four are too few");
            Equal((int?)null, PoiseSearch.AgreedOnly(Enumerable.Repeat<IReadOnlyList<int>>(new[] { 0x9D8, 0xC40 }, 5).ToList()), "two shared places: the head's values stand elsewhere too");
            var mixed = five.Take(4).Append(new[] { 0x9D8, 0x120 }).ToList();
            Equal((int?)0x9D8, PoiseSearch.AgreedOnly(mixed), "a place one actor has by chance does not count");
        }

        /// <summary>An inline wide string at <paramref name="at"/>: seven characters at most, as the game keeps short names.</summary>
        private static void PutWString(byte[] block, int at, string text)
        {
            if (text.Length > InlineCapacity)
            {
                throw new ArgumentException("the test strings are inline", nameof(text));
            }

            Array.Clear(block, at, WStringBytes);
            Encoding.Unicode.GetBytes(text).CopyTo(block, at);
            BitConverter.TryWriteBytes(block.AsSpan(at + 0x10), (long)text.Length);
            BitConverter.TryWriteBytes(block.AsSpan(at + 0x18), (long)InlineCapacity);
        }

        /// <summary>The inline wide string at <paramref name="at"/>; null when the bytes there are no such string.</summary>
        private static string? InlineWString(byte[] block, int at)
        {
            if (at < 0 || at + WStringBytes > block.Length || BitConverter.ToInt64(block, at + 0x18) != InlineCapacity)
            {
                return null;
            }

            var length = BitConverter.ToInt64(block, at + 0x10);
            return length is >= 0 and <= InlineCapacity ? Encoding.Unicode.GetString(block, at, (int)length * 2) : null;
        }
    }
}

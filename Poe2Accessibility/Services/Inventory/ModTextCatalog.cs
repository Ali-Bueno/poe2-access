namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    /// <summary>
    ///     The lines the game writes a modifier with, in every language (catalog/mod-texts.json, `Poe2UiExtract mod-texts`;
    ///     docs/research/file-catalogs.md, "Modifier texts"), so a line written in one language is told apart from its numbers
    ///     and said in another. A line's identity is its English wording with the places left in
    ///     ("{0:+d}% to Fire Resistance"): blocks that word a line alike in English are one line to whoever wrote it. Its
    ///     numbers are the ones the English line shows, one a stat it has a place for. Pure apart from <see cref="Load"/>.
    /// </summary>
    public sealed class ModTextCatalog : IModTexts
    {
        private const string English = "en";
        private const string TextsProperty = "texts", StatsProperty = "stats";

        /// <summary>A number that went through a division and back is rounded to this many decimals: the files show three at most.</summary>
        private const int NumberDecimals = 6;

        // The file's 170,000 lines stay as the UTF-8 they were read as, one after another: as strings they took 130 MB.
        private readonly byte[] pool;
        private readonly int[] lineStart;
        private readonly int[] lineBlock;
        private readonly byte[] lineLanguage;
        private readonly int[] blockFirstLine;
        private readonly int[] blockStats;
        private readonly string[][] blockStatIds;
        private readonly string[] languages;
        private readonly ConcurrentDictionary<(int Block, int Language), ModTextVariation?[]> parsed = new();
        private readonly Lazy<Index> index;
        private readonly Lazy<Dictionary<string, int[]>> byStat;

        /// <summary>The languages a fraction's mark was seen in on a modifier line: "5.9" on the Spanish client, as the English files write it.</summary>
        private static readonly HashSet<string> DecimalMarkSeen = new(StringComparer.Ordinal) { English, "es" };

        private ModTextCatalog(ReadOnlySpan<byte> json)
        {
            var (pool, used) = (new byte[json.Length], 0);
            var (starts, blocks, lineLanguages, firstLines, stats, codes) = (new List<int>(), new List<int>(), new List<byte>(), new List<int>(), new List<string[]>(), new List<string>());
            var reader = new Utf8JsonReader(json);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("not a modifier text catalog");
            }

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var top = reader.GetString();
                reader.Read();
                if (top != TextsProperty || reader.TokenType != JsonTokenType.StartArray)
                {
                    reader.Skip();
                    continue;
                }

                while (reader.Read() && reader.TokenType == JsonTokenType.StartObject)
                {
                    var (block, ids) = (firstLines.Count, Array.Empty<string>());
                    firstLines.Add(starts.Count);
                    while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                    {
                        var name = reader.GetString();
                        reader.Read();
                        if (name == StatsProperty && reader.TokenType == JsonTokenType.String)
                        {
                            ids = reader.GetString()!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        }
                        else if (name == TextsProperty && reader.TokenType == JsonTokenType.StartObject)
                        {
                            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                            {
                                var code = reader.GetString()!;
                                var language = codes.IndexOf(code);
                                if (language < 0)
                                {
                                    language = codes.Count;
                                    codes.Add(code);
                                }

                                reader.Read();
                                if (reader.TokenType != JsonTokenType.StartArray || language > byte.MaxValue)
                                {
                                    reader.Skip();
                                    continue;
                                }

                                while (reader.Read() && reader.TokenType == JsonTokenType.String)
                                {
                                    starts.Add(used);
                                    blocks.Add(block);
                                    lineLanguages.Add((byte)language);
                                    used += reader.CopyString(pool.AsSpan(used)); // unescaped it is never longer than in the file
                                }
                            }
                        }
                        else
                        {
                            reader.Skip();
                        }
                    }

                    stats.Add(ids);
                }
            }

            starts.Add(used);
            firstLines.Add(starts.Count - 1);
            Array.Resize(ref pool, used);
            (this.pool, this.lineStart, this.lineBlock, this.lineLanguage, this.blockFirstLine, this.blockStatIds, this.languages) =
                (pool, starts.ToArray(), blocks.ToArray(), lineLanguages.ToArray(), firstLines.ToArray(), stats.ToArray(), codes.ToArray());
            this.blockStats = this.blockStatIds.Select(ids => ids.Length).ToArray();
            this.index = new Lazy<Index>(this.BuildIndex);
            this.byStat = new Lazy<Dictionary<string, int[]>>(() => this.blockStatIds.SelectMany((ids, block) => ids.Distinct(StringComparer.Ordinal).Select(id => (id, block)))
                .GroupBy(pair => pair.id, pair => pair.block, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal));
        }

        /// <summary>The catalog the plugin loaded; empty until then, so no line is told and none is said.</summary>
        public static ModTextCatalog Shared { get; private set; } = FromJson("{}");

        /// <summary>Gets how many description blocks the catalog holds.</summary>
        public int Count => this.blockStats.Length;

        /// <summary>Gets the languages the blocks are written in.</summary>
        public IReadOnlyList<string> Languages => this.languages;

        /// <summary>Reads the catalog; a missing or unreadable one is logged once and leaves every line untold.</summary>
        public static void Load(PluginLog log, string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    log.Warn($"Modifier text catalog: {path} not found; no modifier line is told");
                    return;
                }

                var catalog = new ModTextCatalog(WithoutMark(File.ReadAllBytes(path)));
                Shared = catalog;
                log.Info($"Modifier text catalog: {catalog.Count} descriptions, {catalog.Languages.Count} languages from the game files");

                // The lines are indexed away from the game's thread: the first line asked for waits only for what is left of it.
                Task.Run(() => catalog.index.Value);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
            {
                log.Warn($"Modifier text catalog unreadable ({error.Message}); no modifier line is told");
            }
        }

        /// <summary>The catalog in <paramref name="json"/>; a JsonException when it is not one.</summary>
        public static ModTextCatalog FromJson(string json) => new(Encoding.UTF8.GetBytes(json));

        /// <inheritdoc/>
        /// <remarks>
        ///     A "reduced" line is another line than its "increased" one, with the number as written. A range alone
        ///     ("+(11-15)%") gives its lower end; a rolled number with its range ("+40(30-40)") the rolled number.
        /// </remarks>
        public ModLine? Parse(string text)
        {
            var (skeleton, numbers) = ModTextVariation.Read(text);
            if (skeleton.Length == 0)
            {
                return null;
            }

            ModLine? found = null;
            foreach (var line in this.index.Value.Hits(skeleton.GetHashCode(StringComparison.Ordinal)))
            {
                if (this.Told(line, skeleton, numbers) is not { } told)
                {
                    continue;
                }

                if (found != null && (found.Template != told.Template || !found.Numbers.SequenceEqual(told.Numbers, Tolerant.Instance)))
                {
                    return null; // two different lines read this way
                }

                found ??= told;
            }

            return found;
        }

        /// <inheritdoc/>
        public string Say(ModLine line, string language)
        {
            var (english, local) = (Array.IndexOf(this.languages, English), Array.IndexOf(this.languages, language));
            if (english < 0 || local < 0 || !this.index.Value.ByTemplate.TryGetValue(line.Template, out var lines))
            {
                return string.Empty;
            }

            foreach (var at in lines)
            {
                var block = this.lineBlock[at];
                if (this.Variation(at) is { } worded && worded.ValuesOf(line.Numbers) is { } values &&
                    ReferenceEquals(this.First(block, english, values), worded) && this.First(block, local, values) is { } said)
                {
                    return said.Say(values);
                }
            }

            return string.Empty;
        }

        /// <summary>The description blocks a stat is in, in the file's order: none for a stat the file does not describe, more than one for the few it describes twice.</summary>
        public IReadOnlyList<int> BlocksOf(string stat) => this.byStat.Value.TryGetValue(stat, out var blocks) ? blocks : Array.Empty<int>();

        /// <summary>The stats a block describes, in the order its lines take their values.</summary>
        public IReadOnlyList<string> StatsOf(int block) => this.blockStatIds[block];

        /// <summary>
        ///     For a stat the file describes in several blocks: the first of them when every one writes the same certain line for
        ///     these values in the language, so the line is that whichever block the game takes (the 26 such stats of 0.5.x are
        ///     each the one stat of each of its blocks); -1 when they write it otherwise, which the file does not settle.
        /// </summary>
        public int AlikeBlock(IReadOnlyList<int> blocks, Func<string, double> value, string language)
        {
            string? written = null;
            foreach (var block in blocks)
            {
                var (text, certain) = this.Show(block, this.blockStatIds[block].Select(value).ToArray(), language);
                if (!certain || text.Length == 0 || (written != null && written != text))
                {
                    return -1;
                }

                written = text;
            }

            return written != null ? blocks[0] : -1;
        }

        /// <summary>
        ///     The line the game writes for a block's stat values in a language: the first of the block they fit. Not certain
        ///     when a line before it cannot be read here (its conditions are then unknown), when none fits, when the language
        ///     has no text for the block, or when a number in it is written a way never seen (<see cref="ModTextVariation.Show"/>).
        /// </summary>
        public (string Text, bool Certain) Show(int block, IReadOnlyList<double> values, string language)
        {
            var local = Array.IndexOf(this.languages, language);
            if (local < 0 || block < 0 || block >= this.blockStatIds.Length || values.Count != this.blockStats[block])
            {
                return (string.Empty, false);
            }

            var unread = false;
            foreach (var variation in this.Variations(block, local))
            {
                if (variation == null)
                {
                    unread = true;
                }
                else if (variation.Fits(values))
                {
                    var (text, certain) = variation.Show(values, DecimalMarkSeen.Contains(language));
                    return (text, certain && !unread);
                }
            }

            return (string.Empty, false);
        }

        private static ReadOnlySpan<byte> WithoutMark(ReadOnlySpan<byte> file) => file.StartsWith(Encoding.UTF8.Preamble) ? file[Encoding.UTF8.Preamble.Length..] : file;

        /// <summary>The line the file's line <paramref name="at"/> tells for these numbers, or null when it is not that line after all.</summary>
        private ModLine? Told(int at, string skeleton, IReadOnlyList<double> numbers)
        {
            var (block, english) = (this.lineBlock[at], Array.IndexOf(this.languages, English));
            if (english < 0 || this.Variation(at) is not { } written || written.Skeleton != skeleton || written.Values(numbers) is not { } values)
            {
                return null;
            }

            // The game writes the first line of the block the values fit: a later one that fits too is never shown for them.
            return ReferenceEquals(this.First(block, this.lineLanguage[at], values), written) && this.First(block, english, values) is { } line
                ? new ModLine(line.Template, line.Numbers(values).Select(number => Math.Round(number, NumberDecimals)).ToArray())
                : null;
        }

        /// <summary>The first line of a block in a language that the values fit, or null.</summary>
        private ModTextVariation? First(int block, int language, IReadOnlyList<double> values) =>
            this.Variations(block, language).FirstOrDefault(variation => variation != null && variation.Fits(values));

        /// <summary>The file's line <paramref name="at"/> as its block's parsed lines hold it.</summary>
        private ModTextVariation? Variation(int at)
        {
            var (block, language) = (this.lineBlock[at], this.lineLanguage[at]);
            var before = 0;
            for (var line = this.blockFirstLine[block]; line < at; line++)
            {
                before += this.lineLanguage[line] == language ? 1 : 0;
            }

            return this.Variations(block, language)[before];
        }

        private ModTextVariation?[] Variations(int block, int language) =>
            this.parsed.GetOrAdd((block, language), key => Enumerable.Range(this.blockFirstLine[key.Block], this.blockFirstLine[key.Block + 1] - this.blockFirstLine[key.Block])
                .Where(line => this.lineLanguage[line] == key.Language).Select(line => ModTextVariation.Parse(this.Line(line), this.blockStats[key.Block])).ToArray());

        private string Line(int at) => Encoding.UTF8.GetString(this.pool, this.lineStart[at], this.lineStart[at + 1] - this.lineStart[at]);

        /// <summary>Every line by its skeleton's hash (the skeleton is compared again on a hit), and the English ones by their wording.</summary>
        private Index BuildIndex()
        {
            var english = Array.IndexOf(this.languages, English);
            var hits = new List<(int Hash, int Line)>(this.lineBlock.Length);
            var byTemplate = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (var at = 0; at < this.lineBlock.Length; at++)
            {
                if (ModTextVariation.Parse(this.Line(at), this.blockStats[this.lineBlock[at]]) is not { } line)
                {
                    continue;
                }

                hits.Add((line.Skeleton.GetHashCode(StringComparison.Ordinal), at));
                if (this.lineLanguage[at] != english)
                {
                    continue;
                }

                if (!byTemplate.TryGetValue(line.Template, out var worded))
                {
                    byTemplate[line.Template] = worded = new List<int>(1);
                }

                worded.Add(at);
            }

            // File order within a hash: the first block that words a line is the one said.
            hits.Sort();
            return new Index(hits.Select(hit => hit.Hash).ToArray(), hits.Select(hit => hit.Line).ToArray(), byTemplate);
        }

        /// <summary>The file's lines sorted by their skeleton's hash, and the English ones by wording.</summary>
        private sealed record Index(int[] Hashes, int[] Lines, Dictionary<string, List<int>> ByTemplate)
        {
            /// <summary>The lines whose skeleton has this hash, in the file's order.</summary>
            public IEnumerable<int> Hits(int hash)
            {
                var at = Array.BinarySearch(this.Hashes, hash);
                if (at < 0)
                {
                    yield break;
                }

                while (at > 0 && this.Hashes[at - 1] == hash)
                {
                    at--;
                }

                for (; at < this.Hashes.Length && this.Hashes[at] == hash; at++)
                {
                    yield return this.Lines[at];
                }
            }
        }

        /// <summary>Numbers that went through a division and back are the same number.</summary>
        private sealed class Tolerant : IEqualityComparer<double>
        {
            public static readonly Tolerant Instance = new();

            public bool Equals(double a, double b) => Math.Abs(a - b) < 1e-6;

            public int GetHashCode(double value) => 0;
        }
    }
}

namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    /// <summary>
    ///     The main passive tree as the game's files hold it (catalog/passive-tree.json, `Poe2UiExtract passive-tree`;
    ///     docs/research/file-catalogs.md): each node's PassiveSkills Id, kind and links by graph id, the classes' start nodes,
    ///     node and class names per language. A patch moves a row's columns in memory, never this. Pure apart from
    ///     <see cref="Load"/>.
    /// </summary>
    public sealed class PassiveTreeCatalog
    {
        private const char FlagSeparator = '|';
        private const char KeystoneFlag = 'k';
        private const char NotableFlag = 'n';
        private const char JewelSocketFlag = 'j';
        private const char IconOnlyFlag = 'i';

        private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

        private readonly Dictionary<int, Node> byGraphId = new();
        private readonly Dictionary<string, Node> byId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Node> startByClass = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<int> graphIds = new();
        private readonly Dictionary<string, Dictionary<string, string>> names;
        private readonly Dictionary<string, Dictionary<string, string>> classes;

        private PassiveTreeCatalog(FileData data)
        {
            this.names = data.Names ?? new();
            // A character's path is compared without case everywhere else (the player's entity path against the table's Id).
            this.classes = (data.Classes ?? new()).ToDictionary(pair => pair.Key, pair => new Dictionary<string, string>(pair.Value, StringComparer.OrdinalIgnoreCase), StringComparer.Ordinal);
            foreach (var (key, value) in data.Nodes ?? new())
            {
                var separator = value.IndexOf(FlagSeparator);
                if (!int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var graphId) || separator <= 0)
                {
                    continue;
                }

                var flags = value[(separator + 1)..];
                var links = data.Links != null && data.Links.TryGetValue(key, out var linked) ? linked.Where(other => other != graphId).ToArray() : Array.Empty<int>();
                var starts = data.Starts != null && data.Starts.TryGetValue(key, out var characters) ? characters.ToArray() : Array.Empty<string>();
                var node = new Node(graphId, value[..separator], flags.Contains(KeystoneFlag), flags.Contains(NotableFlag), flags.Contains(JewelSocketFlag), flags.Contains(IconOnlyFlag), links, starts);
                this.byGraphId[graphId] = node;
                this.byId[node.Id] = node;
                this.graphIds.Add(graphId);
                foreach (var character in starts)
                {
                    this.startByClass[character] = node;
                }
            }

            foreach (var (key, value) in data.Variants ?? new())
            {
                var separator = value.IndexOf(FlagSeparator);
                if (int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var graphId) && separator > 0)
                {
                    this.variants[graphId] = new Variant(graphId, value[..separator], value[(separator + 1)..]);
                    this.variantIds.Add(graphId);
                }
            }
        }

        private const string AttributeVariantType = "GenericAttribute";

        private readonly Dictionary<int, Variant> variants = new();
        private readonly HashSet<int> variantIds = new();

        /// <summary>Gets the graph ids of every row a node with a choice can be made (PassiveSkillVariants); empty in a catalog written before them.</summary>
        public IReadOnlySet<int> VariantIds => this.variantIds;

        /// <summary>What a node with a choice was made, by the graph id the tree's state keeps for it; null when the files know none.</summary>
        public Variant? VariantOf(int graphId) => this.variants.GetValueOrDefault(graphId);

        /// <summary>The attribute an attribute node was given, by its choice's graph id; null for any other choice.</summary>
        public BuildNotes.Attribute? AttributeOf(int variantGraphId) =>
            this.VariantOf(variantGraphId) is not { Type: AttributeVariantType } variant ? null
            : variant.Id.Contains("strength", StringComparison.OrdinalIgnoreCase) ? BuildNotes.Attribute.Strength
            : variant.Id.Contains("dexterity", StringComparison.OrdinalIgnoreCase) ? BuildNotes.Attribute.Dexterity
            : variant.Id.Contains("intelligence", StringComparison.OrdinalIgnoreCase) ? BuildNotes.Attribute.Intelligence
            : null;

        /// <summary>A PassiveSkills row a node becomes by the player's choice ("generic_attribute_strength" of type "GenericAttribute").</summary>
        public sealed record Variant(int GraphId, string Id, string Type);

        /// <summary>The catalog the plugin loaded; empty until then, so every lookup finds nothing and memory is read.</summary>
        public static PassiveTreeCatalog Shared { get; private set; } = new(new FileData());

        /// <summary>Gets how many nodes the catalog knows; 0 without a file.</summary>
        public int Count => this.byGraphId.Count;

        /// <summary>Gets every graph id of the tree, what an allocated list may hold.</summary>
        public IReadOnlySet<int> GraphIds => this.graphIds;

        /// <summary>Reads the catalog; a missing or unreadable one is logged once and leaves the nodes read from memory.</summary>
        public static void Load(PluginLog log, string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    log.Warn($"Passive tree catalog: {path} not found; nodes and links are read from memory");
                    return;
                }

                Shared = Parse(File.ReadAllText(path));
                log.Info($"Passive tree catalog: {Shared.Count} nodes, {Shared.startByClass.Count} classes' starts, {Shared.names.Count} languages from the game files");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
            {
                log.Warn($"Passive tree catalog unreadable ({error.Message}); nodes and links are read from memory");
            }
        }

        /// <summary>The catalog in <paramref name="json"/>; a JsonException when it is not one.</summary>
        public static PassiveTreeCatalog Parse(string json) => new(JsonSerializer.Deserialize<FileData>(json, Json) ?? new FileData());

        /// <summary>
        ///     A node's kind as the plugin says it, the same from the files' flags and from the row's bytes: keystone before
        ///     notable before jewel socket (one node is both notable and socket); a start or an icon is none of them.
        /// </summary>
        public static PassiveClass ClassOf(bool keystone, bool notable, bool jewelSocket, bool iconOnly, bool isStart) =>
            keystone ? PassiveClass.Keystone
            : notable ? PassiveClass.Notable
            : jewelSocket ? PassiveClass.JewelSocket
            : isStart || iconOnly ? PassiveClass.Other
            : PassiveClass.Small;

        /// <summary>The node with this graph id (what the allocated list holds); null when the catalog has none.</summary>
        public Node? ByGraphId(int graphId) => this.byGraphId.GetValueOrDefault(graphId);

        /// <summary>The node with this PassiveSkills Id (a row's first column); null for a node newer than the catalog.</summary>
        public Node? ById(string passiveId) => this.byId.GetValueOrDefault(passiveId);

        /// <summary>The start node of a character class, by the player's metadata path; null when no node starts it.</summary>
        public Node? StartOf(string characterPath) => this.startByClass.GetValueOrDefault(characterPath);

        /// <summary>A node's name in the language, else in English, markup as the game holds it; null for a node with none.</summary>
        public string? Name(string passiveId, string language) => Localization.Pick(this.names, language, passiveId);

        /// <summary>
        ///     Whether the files give the node this name in any language (outer spaces ignored; a node with no name there has
        ///     the empty one): the game's own language need not be the mod's. <paramref name="shown"/> turns a name of the
        ///     files into what the game draws, when the name compared was read off the screen.
        /// </summary>
        public bool IsNameOf(string passiveId, string name, Func<string, string>? shown = null)
        {
            var wanted = name.Trim();
            var named = false;
            foreach (var language in this.names.Values)
            {
                if (language.TryGetValue(passiveId, out var known))
                {
                    named = true;
                    if (string.Equals((shown?.Invoke(known) ?? known).Trim(), wanted, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return !named && wanted.Length == 0;
        }

        /// <summary>A class's name ("Monje") by its Characters Id, in the language, else in English; null when unknown.</summary>
        public string? ClassName(string characterPath, string language) => Localization.Pick(this.classes, language, characterPath);

        /// <summary>One node of the tree; <paramref name="Links"/> are graph ids, both directions, itself left out.</summary>
        public sealed record Node(int GraphId, string Id, bool Keystone, bool Notable, bool JewelSocket, bool IconOnly, IReadOnlyList<int> Links, IReadOnlyList<string> StartClasses)
        {
            /// <summary>Gets a value indicating whether a class starts here (the portrait at the tree's centre).</summary>
            public bool IsStart => this.StartClasses.Count > 0;

            /// <summary>Gets the node's kind.</summary>
            public PassiveClass Class => ClassOf(this.Keystone, this.Notable, this.JewelSocket, this.IconOnly, this.IsStart);
        }

        /// <summary>The file as `passive-tree` writes it.</summary>
        private sealed class FileData
        {
            public Dictionary<string, string>? Nodes { get; set; }

            public Dictionary<string, List<int>>? Links { get; set; }

            public Dictionary<string, List<string>>? Starts { get; set; }

            public Dictionary<string, string>? Variants { get; set; }

            public Dictionary<string, Dictionary<string, string>>? Classes { get; set; }

            public Dictionary<string, Dictionary<string, string>>? Names { get; set; }
        }
    }
}

namespace Poe2Accessibility
{
    using System;
    using System.IO;
    using System.Linq;
    using GameReader;
    using Poe2Accessibility.Services;

    /// <summary>Patch survival of <see cref="Poe2AccessibilityCore"/>: the code fields read from the exe, what a game update added, and the compatibility report.</summary>
    public sealed partial class Poe2AccessibilityCore
    {
        private const string BuffTable = "buffdefinitions";
        private const string NamedKindsPart = "effect names";
        private const string GameDataPart = "game data";
        private const string CodeFieldsPart = "code fields";
        private const string CodeFieldFile = "code-fields.txt";
        private const string WorldAreaStruct = "WorldAreaDatOffsets";
        private const int WorldAreaIdMeasured = 0x0;
        private const int WorldAreaNameMeasured = 0x8;
        private const int WorldAreaIsTownMeasured = 0x14;

        /// <summary>
        ///     The plugin's own offsets the game's code can place (catalog/code-fields.txt, <c>layout:</c>); the getter lets a
        ///     live search that already moved one keep it.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<string, (Func<int> Get, Action<int> Set)> LayoutFields = new()
        {
            ["IsTargetable"] = (() => GameLayout.TargetableComponent.IsTargetableOffset, value => GameLayout.TargetableComponent.IsTargetableOffset = value),
            ["IsTargeted"] = (() => GameLayout.TargetableComponent.IsTargetedOffset, value => GameLayout.TargetableComponent.IsTargetedOffset = value),
            ["MinimapIconComponentRow"] = (() => GameLayout.MinimapIconRow.ComponentRowOffset, value => GameLayout.MinimapIconRow.ComponentRowOffset = GameReader.RemoteObjects.Components.MinimapIcon.RowOffset = value),
            ["SkillId"] = (() => GameLayout.SkillSlot.SkillIdOffset, value => GameLayout.SkillSlot.SkillIdOffset = value),
            ["SkillLevelKey"] = (() => GameLayout.SkillSlot.LevelKeyOffset, value => GameLayout.SkillSlot.LevelKeyOffset = value),
            ["CooldownUses"] = (() => GameLayout.ActorComponent.CooldownUsesOffset, value => GameLayout.ActorComponent.CooldownUsesOffset = value),
            ["CooldownMaxUses"] = (() => GameLayout.ActorComponent.CooldownMaxUsesOffset, value => GameLayout.ActorComponent.CooldownMaxUsesOffset = value),
            ["CooldownTime"] = (() => GameLayout.ActorComponent.CooldownTimeOffset, value => GameLayout.ActorComponent.CooldownTimeOffset = value),
            ["CooldownSkillId"] = (() => GameLayout.ActorComponent.CooldownSkillIdOffset, value => GameLayout.ActorComponent.CooldownSkillIdOffset = value),
            ["CooldownEntrySize"] = (() => GameLayout.ActorComponent.CooldownEntrySize, value => GameLayout.ActorComponent.CooldownEntrySize = value),
            ["SkillEntrySize"] = (() => GameLayout.ActorComponent.SkillEntrySize, value => GameLayout.ActorComponent.SkillEntrySize = value),
            ["PoiseThreshold"] = (() => GameLayout.ActorComponent.PoiseThresholdOffset, value => GameLayout.ActorComponent.PoiseThresholdOffset = value),
            ["Poise"] = (() => GameLayout.ActorComponent.PoiseOffset, value => GameLayout.ActorComponent.PoiseOffset = value),
            ["NodeInBuild"] = (() => GameLayout.PassiveTree.NodeInBuildOffset, value => GameLayout.PassiveTree.NodeInBuildOffset = value),
            ["ButtonMenuIndex"] = (() => GameLayout.ButtonMenu.SelectedIndexOffset, value => GameLayout.ButtonMenu.SelectedIndexOffset = value),
            ["PlayerStride"] = (() => GameLayout.UiManager.PlayerStride, value => GameLayout.UiManager.PlayerStride = value),
            ["ModRowOffset"] = (() => GameLayout.ModArray.RowOffset, value => GameLayout.ModArray.RowOffset = value),
            ["ModEntrySize"] = (() => GameLayout.ModArray.EntrySize, value => GameLayout.ModArray.EntrySize = value),
            ["StateListOffset"] = (() => GameReader.RemoteObjects.Components.StateMachine.StateListOffset, value => GameReader.RemoteObjects.Components.StateMachine.StateListOffset = value),
            ["StateStride"] = (() => GameReader.RemoteObjects.Components.StateMachine.StateStride, value => GameReader.RemoteObjects.Components.StateMachine.StateStride = value),
        };

        /// <summary>
        ///     Each of those offsets as compiled, taken before anything could move one: a new game process starts from them
        ///     again. Left where the last process's code or override put them, a patched game restarted under the same host
        ///     would be read through the old build's offsets, each "kept where a live search put it".
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<string, int> LayoutMeasured = LayoutFields.ToDictionary(field => field.Key, field => field.Value.Get());

        private string? gameNewsLine;

        /// <summary>Judgments of the left trigger that must find no button bit before the pad's buttons are said lost.</summary>
        private const int PadMissesToSay = 2;

        private int padMisses;

        private const string SemanticFieldFile = "semantic-fields.txt";

        /// <summary>The code fields of this game process, being read off the frame thread.</summary>
        private System.Threading.Tasks.Task<CodeFieldReading>? codeFields;

        /// <summary>The hand-written fields for this game build (config/layout-overrides.txt), applied with the code fields.</summary>
        private System.Collections.Generic.IReadOnlyList<(CodeFieldEntry Entry, int? Found)> layoutOverrides = Array.Empty<(CodeFieldEntry, int?)>();

        /// <summary>Per frame: whether the pad's button bits are where measured, judged on the left trigger (PadButtonCheck).</summary>
        private void CheckPadButtons(GamepadReader.PadState state)
        {
            if (!this.padCheck.Observe(state.Buttons, state.LeftTrigger, state.RightTrigger, GameLayout.PadButtons.MeasuredLeftTriggerBit))
            {
                return;
            }

            if (this.padCheck.Shift is not { } shift)
            {
                // One judgment that finds no bit (frames read while the game ignored the pad) is made again before it is said.
                if (++this.padMisses >= PadMissesToSay)
                {
                    this.compatibility?.Missing("pad buttons");
                }
                else
                {
                    this.padCheck.Reset();
                }

                return;
            }

            GameLayout.PadButtons.Shift = shift;
            if (shift == 0)
            {
                this.compatibility?.Found("pad buttons");
            }
            else
            {
                this.compatibility?.Moved("pad buttons", GameLayout.PadButtons.MeasuredLeftTriggerBit, GameLayout.PadButtons.MeasuredLeftTriggerBit + shift);
            }
        }

        /// <summary>Per frame: the parts of the game's layout that could not be found, said once together.</summary>
        private void SayCompatibility()
        {
            this.TakeCodeFields();
            this.worldLayout?.Tick(this.InGame);
            if (this.compatibility?.TakeUnsaid() is { Count: > 0 } missing)
            {
                this.output.Speak(Announcements.CompatibilityMissing(missing), false);
            }

            if (this.worldLayout?.TakeSurvived() is { } survived)
            {
                this.output.Speak(survived, false);
            }

            if (this.gameNewsLine is { } news && this.InGame)
            {
                this.gameNewsLine = null;
                this.output.Speak(news, false);
                this.RememberGameKeys();
            }
        }

        private string GameKeysCatalog => Path.Join(this.DllDirectory, "catalog", "game-keys.txt");

        private string KnownGameKeys => Path.Join(this.DllDirectory, "config", "game-keys-known.txt");

        private void RememberGameKeys()
        {
            try
            {
                GameNews.Remember(this.GameKeysCatalog, this.KnownGameKeys);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                this.log?.Info($"Game news: could not keep the list: {error.Message}");
            }
        }

        /// <summary>Every kind the mod's rules name by id must still exist in the game's tables; a rename is said, not left silent.</summary>
        private void CheckNamedKinds(string catalog)
        {
            if (!File.Exists(catalog))
            {
                return;
            }

            var buffs = GameNews.Keys(File.ReadLines(catalog), BuffTable);
            if (buffs.Count == 0)
            {
                return; // the table was not read, which says nothing of what is in it
            }

            var animations = GameIds.Current.AnimationNames.ToList();
            var charges = Enum.GetValues<ChargeKind>().Where(kind => kind != ChargeKind.Rage); // a stat, not a buff
            var buffStates = Enum.GetValues<EnemyState>().Where(state => state is not (EnemyState.PrimedForStun or EnemyState.HeavyStunned));
            var animationStates = new[] { EnemyState.HeavyStunned, EnemyState.Electrocuted };
            var lost = GameNews.Unmatched(buffs, charges, PlayerCharges.FromBuffId).Select(kind => kind.ToString())
                .Concat(GameNews.Unmatched(buffs, buffStates, EnemyStates.FromBuffId).Select(state => state.ToString()))
                .Concat(GameNews.Unmatched(buffs, Enum.GetValues<StatusAilment>(), StatusAilments.FromBuffId).Select(ailment => ailment.ToString()))
                .Concat(animations.Count == 0 ? Array.Empty<string>() : GameNews.Unmatched(animations, animationStates, EnemyStates.FromAnimation).Select(state => $"{state} animation"))
                .ToList();
            if (lost.Count == 0)
            {
                this.compatibility?.Found(NamedKindsPart);
            }
            else
            {
                this.log?.Info($"Game news: no id in the game reads as {string.Join(", ", lost)}");
                this.compatibility?.Missing(NamedKindsPart);
            }
        }

        /// <summary>
        ///     Once per game process, after the report's reset: the kinds the mod names, the catalogs a patch left behind, and what an
        ///     update added, which is logged, listed and said once in play. Files in use must not stop anything: they only cost the news.
        /// </summary>
        private void CheckGameNews()
        {
            try
            {
                this.CheckNamedKinds(this.GameKeysCatalog);
                var failed = Path.Join(this.DllDirectory, "catalog", "game-data-failed.txt");
                if (File.Exists(failed))
                {
                    this.log?.Info($"Game news: not regenerated after the update: {string.Join(", ", File.ReadAllLines(failed))}");
                    this.compatibility?.Missing(GameDataPart);
                }

                var notes = GameNewsNotes.Load(Path.Join(this.DllDirectory, "catalog"));
                var news = GameNews.Check(this.GameKeysCatalog, this.KnownGameKeys, Path.Join(this.DllDirectory, "config", "game-news.txt"), notes.Of);
                var summary = GameNews.Summary(news, notes.Of);
                foreach (var (table, added, unclassified) in summary)
                {
                    this.log?.Info($"Game news: {table} {added} new ({unclassified} unclassified), {news.First(item => item.Table == table).Removed.Count} gone");
                }

                this.gameNewsLine = Announcements.GameNews(summary);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                this.log?.Info($"Game news: not checked: {error.Message}");
            }

            if (this.gameNewsLine == null)
            {
                this.RememberGameKeys();
            }
        }

        /// <summary>The exe of the last game process under this host (size and write time); empty before the first.</summary>
        private string lastGameBuild = string.Empty;

        /// <summary>Logs which build of the game this is; a new game process is checked against the layout again.</summary>
        private void NoteGameBuild()
        {
            this.inventoryReader?.ResetNameWords();
            this.compatibility?.Reset();
            DatColumns.Report(this.compatibility); // said once per game process, and the reset forgot it
            this.CheckGameNews();
            foreach (var (name, field) in LayoutFields)
            {
                field.Set(LayoutMeasured[name]);
            }

            this.worldLayout?.Reset();
            this.layoutProbe?.Reset();
            this.locator?.Fields.Reset();
            this.controls?.Reset();
            this.entityFields?.Reset();
            this.flagBits?.Reset();
            this.bosses?.ResetLayout();
            SkillLevelKey.Reset();
            this.alerts?.Cooldowns.ResetLayout();
            this.padCheck.Reset();
            this.padMisses = 0;
            this.semantics?.TreeFields.Reset();
            this.semantics?.StickCheck.Reset();
            this.places?.ResetLayout();
            this.inventoryReader?.BaseNames.Reset();
            GameLayout.PadButtons.Shift = 0;
            this.gameState?.ResetLayout();
            this.locator?.Reset();
            try
            {
                using var game = System.Diagnostics.Process.GetProcessById((int)Core.Process.Pid);
                if (game.MainModule?.FileName is { Length: > 0 } path)
                {
                    var file = new FileInfo(path);
                    var build = $"{file.Length}:{file.LastWriteTimeUtc.Ticks}";
                    if (GameBuildChange.Patched(this.lastGameBuild, build))
                    {
                        // The catalogs were made when the mod started, for the build that ran then: said, because the
                        // launcher's own notice of it is only a log line (the plugin holds the speech by now).
                        this.log?.Info("Game build changed under the running mod: its game data is the last build's");
                        this.output?.Speak(Announcements.GameUpdatedUnderMod, false);
                    }

                    this.lastGameBuild = build;
                    if (this.worldLayout != null)
                    {
                        this.worldLayout.GameBuild = $"{file.Length}:{file.LastWriteTimeUtc.Ticks}";
                    }

                    this.StartCodeFields(path);
                    this.log?.Info($"Game build: {path}, {file.Length} bytes, written {file.LastWriteTimeUtc:yyyy-MM-dd HH:mm} UTC");
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                this.log?.Debug($"Game build unreadable: {ex.Message}");
            }
        }

        /// <summary>The host reads WorldAreas rows by struct: its columns follow the table as the plugin's own do.</summary>
        private static void PlaceWorldAreaColumns(System.Collections.Generic.IReadOnlyDictionary<string, int>? columns)
        {
            var fields = new System.Collections.Generic.List<(string Field, int Expected, int Found)>();
            if (columns?.TryGetValue("WorldAreas.Id", out var id) == true)
            {
                fields.Add(("IdPtr", WorldAreaIdMeasured, id));
            }

            if (columns?.TryGetValue("WorldAreas.Name", out var name) == true)
            {
                fields.Add(("NamePtr", WorldAreaNameMeasured, name));
            }

            if (columns?.TryGetValue("WorldAreas.IsTown", out var town) == true)
            {
                fields.Add(("IsTown", WorldAreaIsTownMeasured, town));
            }

            if (fields.Count > 0)
            {
                GameReader.Utils.OffsetHealer.PlaceFromCode(WorldAreaStruct, fields);
            }
        }

        /// <summary>
        ///     Reads, off the frame thread, where the game's code holds the fields no live check judges: the byte signatures
        ///     and the semantic rules (docs/research/exe-layouts.md). <see cref="TakeCodeFields"/> follows what moved.
        /// </summary>
        private void StartCodeFields(string exe)
        {
            var catalog = Path.Join(this.DllDirectory, "catalog");
            this.layoutOverrides = this.ReadLayoutOverrides(new FileInfo(exe).Length);
            this.codeFields = System.Threading.Tasks.Task.Run(() => ReadCodeFields(exe, catalog));
        }

        /// <summary>The hand-written fields for this build (<see cref="LayoutOverrides"/>); none when there is no file.</summary>
        private System.Collections.Generic.IReadOnlyList<(CodeFieldEntry Entry, int? Found)> ReadLayoutOverrides(long exeSize)
        {
            var path = Path.Join(this.DllDirectory, "config", LayoutOverrides.FileName);
            try
            {
                if (!File.Exists(path))
                {
                    return Array.Empty<(CodeFieldEntry, int?)>();
                }

                var fields = LayoutOverrides.Parse(File.ReadLines(path), exeSize, out var otherBuilds, out var rejected);
                this.log?.Info($"Layout overrides: {fields.Count} for this build ({exeSize} bytes), {otherBuilds} for other builds, not used"
                    + (rejected.Count == 0 ? string.Empty : $"; unreadable: {string.Join(" | ", rejected)}"));
                return fields;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                this.log?.Warn($"Layout overrides unread: {ex.Message}");
                return Array.Empty<(CodeFieldEntry, int?)>();
            }
        }

        /// <summary>
        ///     Per frame: once the reading is done, follows what moved, as a patch that moved a field would otherwise read
        ///     wrong in silence. A field a live judgment already placed stays (<c>OffsetHealer.PlaceFromCode</c>).
        /// </summary>
        private void TakeCodeFields()
        {
            if (this.codeFields is not { IsCompleted: true } task)
            {
                return;
            }

            this.codeFields = null;
            var reading = task.IsCompletedSuccessfully ? task.Result
                : new CodeFieldReading(Array.Empty<(CodeFieldEntry, int?)>(), 0, Array.Empty<(CodeFieldEntry, int?)>(), 0, task.Exception?.GetBaseException().Message, null);
            if (reading.Error != null)
            {
                this.log?.Warn($"Code fields unread: {reading.Error}");
                if (this.layoutOverrides.Count == 0)
                {
                    return;
                }
            }

            var (settled, disagree) = CodeFieldCatalog.Settle(reading.Code, reading.Semantic);
            var read = LayoutOverrides.Apply(settled, this.layoutOverrides);
            var moved = new System.Collections.Generic.List<string>();
            var stuck = false;
            foreach (var group in read.Where(field => field.Found != null && field.Entry.Struct != null).GroupBy(field => field.Entry.Struct!))
            {
                var fields = group.Select(field => (field.Entry.Field, field.Entry.Measured, field.Found!.Value)).Distinct().ToList();
                if (GameReader.Utils.OffsetHealer.PlaceFromCode(group.Key, fields) is not { } outcomes)
                {
                    this.log?.Info($"Code fields: {group.Key} does not match the catalog, left where it is");
                    continue;
                }

                moved.AddRange(outcomes.Where(outcome => outcome.Health == GameReader.Utils.StructHealth.Moved)
                    .Select(outcome => $"{group.Key}.{outcome.Name} +0x{outcome.From:X} to +0x{outcome.To:X}"));
            }

            foreach (var (entry, found) in read)
            {
                if (found is not { } value || value == entry.Measured)
                {
                    continue;
                }

                if (entry.Layout is { } layout && LayoutFields.TryGetValue(layout, out var field))
                {
                    // Memory beats code: a live search that moved the field before the code was read keeps its value.
                    if (field.Get() != entry.Measured)
                    {
                        this.log?.Info($"Code fields: {entry.Name} reads +0x{value:X}, kept at +0x{field.Get():X} where a live search put it");
                        continue;
                    }

                    field.Set(value);
                    moved.Add($"{entry.Name} +0x{entry.Measured:X} to +0x{value:X}");
                }
                else if (entry.Layout is { } unknown)
                {
                    // An override or a catalog line naming an offset the plugin does not have changes nothing: said in the log,
                    // or whoever wrote it believes it is in force.
                    this.log?.Warn($"Code fields: {entry.Name} names the plugin offset '{unknown}', which does not exist: +0x{value:X} is NOT applied");
                }
                else if (entry.IsReport)
                {
                    stuck = true;
                    this.log?.Info($"Code fields: {entry.Name} moved from +0x{entry.Measured:X} to +0x{value:X}, and nothing can follow it");
                }
            }

            var semanticMoves = reading.Semantic.Where(field => field.Found is { } value && value != field.Entry.Measured)
                .Select(field => $"{field.Entry.Name} +0x{field.Entry.Measured:X} to +0x{field.Found:X}").Distinct().ToList();
            this.log?.Info(Tally("Code fields", reading.Code, reading.CodeMilliseconds, moved));
            PassiveStick.Apply(reading.StickRule);
            this.log?.Info(reading.StickRule is { } rule
                ? $"Stick rule: cos {rule.LinkedCosine} slope {rule.LinkedSlope} base {rule.LinkedBase}, cone cos {rule.ConeCosine} slope {rule.ConeSlope} read from the exe{(PassiveStick.Retuned ? ", retuned" : ", as measured")}"
                : "Stick rule: its code not found in the exe, the measured constants stand");
            this.log?.Info(reading.SemanticError is { } error ? $"Semantic fields unread: {error}"
                : Tally("Semantic fields", reading.Semantic, reading.SemanticMilliseconds, semanticMoves)
                    + (disagree.Count == 0 ? string.Empty : $"; disagree, left where they are: {string.Join(", ", disagree)}"));
            // A field neither reading settles keeps its constant, right or wrong: on the build the catalogs were made for every
            // one settles, so this is a patch that changed the code around it, and many of these fields have no other check.
            var unsettled = CodeFieldCatalog.Unsettled(read);
            var heard = CodeFieldCatalog.Heard(unsettled);
            if (unsettled.Count > 0)
            {
                this.log?.Warn($"Code fields: {unsettled.Count} read by neither the signatures nor the rules, their constants stand unchecked: {string.Join(", ", unsettled)}" +
                    (heard.Count == unsettled.Count ? string.Empty : $"; said for {(heard.Count == 0 ? "none (the rest are judged live or not read by the mod)" : string.Join(", ", heard))}"));
            }

            // Said only for a field that has no other judge: the others are found, or said lost, by their own live checks.
            if (stuck || heard.Count > 0)
            {
                this.compatibility?.Missing(CodeFieldsPart);
            }
        }

        /// <summary>"Code fields: 50 of 50 read in 600 ms; moved: none", and what read nothing.</summary>
        private static string Tally(
            string source, System.Collections.Generic.IReadOnlyList<(CodeFieldEntry Entry, int? Found)> read, long milliseconds, System.Collections.Generic.IReadOnlyList<string> moved)
        {
            var unsettled = read.Where(field => field.Found == null).Select(field => field.Entry.Name).Distinct().ToList();
            return $"{source}: {read.Count(field => field.Found != null)} of {read.Count} read in {milliseconds} ms; moved: {(moved.Count == 0 ? "none" : string.Join(", ", moved))}"
                + (unsettled.Count == 0 ? string.Empty : $"; not settled: {string.Join(", ", unsettled)}");
        }

        /// <summary>The semantic rules' reading, or why there is none: a reader that fails leaves the code fields standing.</summary>
        private static (System.Collections.Generic.IReadOnlyList<(CodeFieldEntry Entry, int? Found)> Read, string? Error) ReadSemanticFields(byte[] file, string rules)
        {
            try
            {
                return (CodeFieldCatalog.ReadSemantic(file, CodeFieldCatalog.ParseSemantic(File.ReadLines(rules))), null);
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or IndexOutOfRangeException or InvalidOperationException or OverflowException)
            {
                return (Array.Empty<(CodeFieldEntry, int?)>(), ex.Message);
            }
        }

        /// <summary>Both readings of the exe on disk; nothing touches the game. For a worker thread.</summary>
        private static CodeFieldReading ReadCodeFields(string exe, string catalog)
        {
            try
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var entries = CodeFieldCatalog.Parse(File.ReadLines(Path.Join(catalog, CodeFieldFile)));
                var file = File.ReadAllBytes(exe);
                var (start, length) = GameReader.Utils.CodeFieldSearch.CodeOf(file);
                var code = CodeFieldCatalog.Read(file.AsSpan(start, length), entries);
                var codeTime = watch.ElapsedMilliseconds;
                var (semantic, semanticError) = ReadSemanticFields(file, Path.Join(catalog, SemanticFieldFile));
                return new CodeFieldReading(code, codeTime, semantic, watch.ElapsedMilliseconds - codeTime, null, semanticError) { StickRule = StickRuleCode.Read(file) };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                return new CodeFieldReading(Array.Empty<(CodeFieldEntry, int?)>(), 0, Array.Empty<(CodeFieldEntry, int?)>(), 0, ex.Message, null);
            }
        }

        /// <summary>What the exe on disk says, from the signatures and from the semantic rules, and how long each took.</summary>
        private sealed record CodeFieldReading(
            System.Collections.Generic.IReadOnlyList<(CodeFieldEntry Entry, int? Found)> Code,
            long CodeMilliseconds,
            System.Collections.Generic.IReadOnlyList<(CodeFieldEntry Entry, int? Found)> Semantic,
            long SemanticMilliseconds,
            string? Error,
            string? SemanticError)
        {
            /// <summary>The stick rule's constants as this exe holds them; null when its code was not found.</summary>
            public StickRuleCode.Rule? StickRule { get; init; }
        }
    }
}

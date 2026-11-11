namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The names the plugin's rules match, checked against the game's files by the data step "game names".</summary>
    public static class GameNameTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ANameThePatchRenamedIsSaidWithItsRule), ANameThePatchRenamedIsSaidWithItsRule),
            (nameof(AnObjectsStateCountsWhereverItsScriptsDeclareIt), AnObjectsStateCountsWhereverItsScriptsDeclareIt),
            (nameof(EveryRulesNameIsChecked), EveryRulesNameIsChecked),
            (nameof(TheShippedCheckFoundEveryName), TheShippedCheckFoundEveryName),
        };

        private const string Door = "Metadata/Objects/Door";

        private static GameNameCheck.Files Files(IEnumerable<string> animations, IReadOnlyDictionary<string, string>? scripts = null) => new(
            new HashSet<string> { "dazed" }, animations.ToHashSet(), new[] { "Metadata/Monsters/MudBurrower/MudBurrowerTailBoss_" },
            path => scripts != null && scripts.TryGetValue(path, out var text) ? text : null);

        private static readonly GameName[] Names =
        {
            new(GameNameKind.Buff, "dazed", null, "EnemyStates"),
            new(GameNameKind.Animation, "HeavyStun", null, "EnemyStates"),
            new(GameNameKind.AnimationStart, "ActiveBlockChannel", null, "ShieldStance"),
            new(GameNameKind.Monster, "MudBurrowerTailBoss", null, "WeakPoints"),
        };

        private static void ANameThePatchRenamedIsSaidWithItsRule()
        {
            Expect(0, GameNameCheck.Gone(Names, Files(new[] { "HeavyStun", "ActiveBlockChannelMoving" })).Count, "every name held: an Id, a start, a word of a monster's file");
            var gone = GameNameCheck.Gone(Names, Files(new[] { "HeavyStunned", "ActiveBlockChannelMoving" }));
            Expect("gone from the game's files: Animation HeavyStun (EnemyStates)", GameNameCheck.Line(gone), "the renamed animation and the rule it silences");
            Expect(1, GameNameCheck.Gone(Names, Files(new[] { "HeavyStun", "RaiseShieldMoving" })).Count, "no Id starts as the shield's did");
        }

        private static void AnObjectsStateCountsWhereverItsScriptsDeclareIt()
        {
            var scripts = new Dictionary<string, string>
            {
                [Door] = "version 3\nextends \"Metadata/Objects/DoorBase\"\nStateMachine\n{\n\tdefine_shared_state = \"wall;\"\n}\n",
                ["Metadata/Objects/DoorBase"] = "version 3\nextends \"nothing\"\nStateMachine\n{\n\tdefine_shared_state = \"open; trap_speed = 2;\"\n}\n",
            };
            var files = Files(Array.Empty<string>(), scripts);
            Expect(true, GameNameCheck.Holds(new GameName(GameNameKind.ObjectFile, Door, "open", "TrialDoors"), files), "declared by the script it extends");
            Expect(true, GameNameCheck.Holds(new GameName(GameNameKind.ObjectFile, Door, null, "TrialTraps"), files), "a file alone needs only to exist");
            Expect(false, GameNameCheck.Holds(new GameName(GameNameKind.ObjectFile, Door, "closed", "TrialDoors"), files), "a state no script declares");
            Expect(false, GameNameCheck.Holds(new GameName(GameNameKind.ObjectFile, "Metadata/Objects/Gone", null, "TrialTraps"), files), "a script gone");
            Expect(false, GameNameCheck.Holds(new GameName(GameNameKind.ObjectFile, Door, "trap_speed = 2", "TrialDoors"), files), "a default is no part of the name");
        }

        private static void EveryRulesNameIsChecked()
        {
            var names = GameNames.All().ToList();
            Expect(true, EnemyStates.BuffIdsMatched.All(id => names.Contains(new GameName(GameNameKind.Buff, id, null, nameof(EnemyStates)))), "every enemy-state buff");
            Expect(true, EnemyStates.AnimationsMatched.All(id => names.Contains(new GameName(GameNameKind.Animation, id, null, nameof(EnemyStates)))), "every enemy-state animation");
            Expect(true, TrialTraps.FilesMatched.All(trap => names.Any(name => name.Kind == GameNameKind.ObjectFile && name.Value == trap.File) &&
                                                             trap.Firing.All(state => names.Any(name => name.Value == trap.File && name.State == state))), "every trap file and strike state");
            Expect(true, names.Contains(new GameName(GameNameKind.ObjectFile, TrialDoors.DoorFile, TrialDoors.OpenState, nameof(TrialDoors))), "the room door's open state");
            Expect(true, names.Any(name => name.Value == ShieldStance.HeldPrefix) && names.Any(name => name.Value == ShieldStance.EndPrefix), "the shield's two animations");
            Expect(true, GameNames.KnownTails.All(tail => WeakPoints.IsTail("Metadata/Monsters/X/" + tail + "_")), "each known tail is a tail to the rule");
        }

        private static void TheShippedCheckFoundEveryName()
        {
            // The shipped file is the step's output on the game's files: every name a rule matches was found there.
            var root = AppContext.BaseDirectory;
            while (root is not null && !File.Exists(Path.Combine(root, "Poe2Access.slnx")))
            {
                root = Path.GetDirectoryName(root);
            }

            var shipped = File.ReadAllLines(Path.Combine(root!, "Poe2Accessibility", "catalog", "game-names.txt")).Where(line => !line.StartsWith('#')).ToList();
            var expected = GameNames.All().Select(name => $"{name.Kind}\t{name.Value}\t{name.State}\t{name.Rule}").ToList();
            Expect(string.Join("\n", expected), string.Join("\n", shipped), "regenerate catalog/game-names.txt when a rule's names change");
        }
    }
}

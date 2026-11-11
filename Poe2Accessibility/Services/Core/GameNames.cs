namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Where in the game's files a name the plugin matches lives.</summary>
    public enum GameNameKind
    {
        /// <summary>A BuffDefinitions Id.</summary>
        Buff,

        /// <summary>An Animation Id.</summary>
        Animation,

        /// <summary>The start of one Animation Id or more.</summary>
        AnimationStart,

        /// <summary>Part of the last segment of a MonsterVarieties Id.</summary>
        Monster,

        /// <summary>An object's script (.ot) and, when given, a state its StateMachine declares there or in a script it extends.</summary>
        ObjectFile,
    }

    /// <param name="State">For an object's script, the state matched on it; null when only the file is.</param>
    /// <param name="Rule">The rule that matches the name, said when the name is gone.</param>
    public readonly record struct GameName(GameNameKind Kind, string Value, string? State, string Rule);

    /// <summary>
    ///     The game's names the plugin's rules match by their text, each with its rule: the data step "game names" checks each
    ///     is still in the game's files after a patch, so a rename fails that step and is said instead of a rule falling
    ///     silent (docs/patch-day.md). Pure.
    /// </summary>
    public static class GameNames
    {
        /// <summary>The tails the weak-point rule was made from (MonsterVarieties of 0.5.x); each carries WeakPoints.TailWord.</summary>
        public static readonly string[] KnownTails = { "MudBurrowerTailBoss", "DevourerBossDuoTail" };

        public static IEnumerable<GameName> All() =>
            EnemyStates.BuffIdsMatched.Select(id => new GameName(GameNameKind.Buff, id, null, nameof(EnemyStates)))
                .Concat(EnemyStates.AnimationsMatched.Select(id => new GameName(GameNameKind.Animation, id, null, nameof(EnemyStates))))
                .Concat(new[] { ShieldStance.HeldPrefix, ShieldStance.EndPrefix }.Select(start => new GameName(GameNameKind.AnimationStart, start, null, nameof(ShieldStance))))
                .Concat(PerfectTimingChannel.Channels.Select(id => new GameName(GameNameKind.Animation, id, null, nameof(PerfectTimingChannel))))
                .Concat(KnownTails.Select(tail => new GameName(GameNameKind.Monster, tail, null, "WeakPoints")))
                .Concat(TrialTraps.FilesMatched.SelectMany(trap => trap.Firing.Count == 0
                    ? new[] { new GameName(GameNameKind.ObjectFile, trap.File, null, nameof(TrialTraps)) }
                    : trap.Firing.Select(state => new GameName(GameNameKind.ObjectFile, trap.File, state, nameof(TrialTraps)))))
                .Append(new GameName(GameNameKind.ObjectFile, TrialDoors.DoorFile, TrialDoors.OpenState, nameof(TrialDoors)));
    }
}

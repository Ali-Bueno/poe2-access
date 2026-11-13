using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>An ExecuteGEAL row: the objects it acts on (those that are monsters apart), the script run on the caster and the commands run on each object it hits.</summary>
internal sealed record BossGeal(string Id, IReadOnlyList<string> Objects, string Self, string Each, IReadOnlyList<string> Monsters);

/// <summary>
///     One skill as the census rules read it: its ActiveSkills kind, whether it has a danger area (a GeometryAttack or
///     GeometryTrigger row) and its length, the ExecuteGEAL rows it runs, the objects it spawns, its daemons and summons, the
///     buffs it gives, the skills it triggers, and whether it cannot be dodged, evaded or blocked.
/// </summary>
internal sealed record BossSkill(
    string Id, string Kind, bool Area, int Length, IReadOnlyList<BossGeal> Geals, IReadOnlyList<string> Objects, IReadOnlyList<string> Daemons,
    IReadOnlyList<string> Summons, IReadOnlyList<string> Buffs, IReadOnlyList<string> Triggers, bool Unavoidable);

/// <summary>A buff linked to a fight: its id, its name and description as one text, whether the game shows its count, whether its stats harm.</summary>
internal sealed record BossBuff(string Id, string Text, bool ShowCount, bool Harmful);

/// <summary>
///     A monster a fight brings: preloaded, summoned, a skill's move-daemon, or an NPC sharing the boss's skills; in the arena
///     when the boss brings it or the arena's rooms place it (an NPC sharing skills may be another act's).
/// </summary>
internal sealed record BossAdd(string Id, string Name, bool BossBar, IReadOnlyList<string> SkillIds, IReadOnlyList<BossSkill> Skills, BossScriptChain Script, bool Daemon, bool InArena = true);

/// <summary>An object of a fight (preloaded, spawned, acted on by its skills, beside its script, placed in its arena) and its script.</summary>
internal sealed record BossObject(string Path, BossScriptChain Script);

/// <summary>
///     One boss-bar monster's fight as the census rules read it (docs/research/boss-mechanics.md §14): its script chain, its
///     skills (with those they trigger), its adds, its objects, the server's ExecuteGEAL rows named for its people, its linked buffs.
/// </summary>
internal sealed record BossFight(
    string Boss, BossScriptChain Script, IReadOnlyList<BossSkill> Skills, IReadOnlyList<BossAdd> Adds, IReadOnlyList<BossObject> Objects,
    IReadOnlyList<BossGeal> Orphans, IReadOnlyList<BossBuff> Buffs)
{
    /// <summary>Every ExecuteGEAL row of the fight: its own skills', its adds' skills' and the server's ones named for its people.</summary>
    public IEnumerable<BossGeal> Geals => this.Skills.SelectMany(skill => skill.Geals).Concat(this.Adds.SelectMany(add => add.Skills).SelectMany(skill => skill.Geals))
        .Concat(this.Orphans).DistinctBy(geal => geal.Id, StringComparer.Ordinal);
}

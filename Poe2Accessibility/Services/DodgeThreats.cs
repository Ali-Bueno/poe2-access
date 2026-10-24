namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>What a monster's animation threatens: nothing, a blow that needs it beside the player, or a hit from afar.</summary>
    public enum DodgeThreat
    {
        None,
        Melee,
        Ranged,
    }

    /// <summary>
    ///     Sorts the game's animation names (GameHelper's <c>Animation</c> enum, from the game's Animation table) for the
    ///     dodge warning. Pure. A name must say harm to warn: the Mud Burrow's grub warned on Submerge and Hide every
    ///     second (2026-10-24) when anything not calm counted as an attack.
    /// </summary>
    public static class DodgeThreats
    {
        /// <summary>Moving, reacting, hiding, emoting, buffing or summoning: nothing aimed at the player.</summary>
        private static readonly string[] Calm =
        {
            "Idle", "Run", "Walk", "TakeHit", "Death", "Dead", "Stun", "Spawn", "Emerge", "Flinch", "Knockback", "Knockdown", "Turn",
            "Taunt", "Sleep", "Rise", "Buff", "Submerge", "Hide", "Dodge", "Sprint", "Movement", "Move", "Look", "Emote", "Interact",
            "Mount", "Summon", "Command", "Mark", "Block", "Parry", "Revive", "Resurrect", "Climb", "ChangeAmmo", "Expire",
            "Meditate", "Pant", "Dance", "Float", "Sit", "GetUp", "Getup", "Stance", "Swap", "Teleport", "Warcry", "Banner",
        };

        /// <summary>Blows that land beside the monster: a threat only once it is within reach.</summary>
        private static readonly string[] MeleeWords =
        {
            "Melee", "Strike", "Slash", "Slam", "Cleave", "Chop", "Stomp", "Smash", "Bite", "Claw", "Swipe", "Combo", "Stab", "Bash",
            "Rake", "Maul", "Punch", "Kick", "Sweep", "Whirl", "Spin", "Impale", "Execute", "Puncture", "Reap", "Hammer", "Sunder",
            "Flurry", "Crash", "Attack", "Backstab",
        };

        /// <summary>Hits that cross the distance: projectiles, spells, areas, and leaps or charges at the player.</summary>
        private static readonly string[] RangedWords =
        {
            "Projectile", "Spell", "Cast", "Channel", "Shot", "Shoot", "Throw", "Toss", "Spit", "Breath", "Nova", "Bolt", "Blast", "Beam",
            "Leap", "Lunge", "Charge", "Dash", "Pounce", "Fling", "Grenade", "Volley", "Barrage", "Rain", "Storm", "Wave", "Comet",
            "Fireball", "Flame", "Cascade", "Earthquake", "Orb", "Wall", "AreaOfEffect", "Aoe", "Rampage", "Devour", "Arrow",
        };

        /// <summary>The threat of an animation id; an id missing from the table (a game update) is taken as a hit from afar.</summary>
        public static DodgeThreat Of(int animation, Func<int, string?> nameOf)
        {
            if (animation <= 0)
            {
                return DodgeThreat.None;
            }

            if (nameOf(animation) is not { } name)
            {
                return DodgeThreat.Ranged;
            }

            if (Contains(name, Calm))
            {
                return DodgeThreat.None;
            }

            // A leap or a charge at the player hits from afar even when its name also says strike ("LeapAttack").
            return Contains(name, RangedWords) ? DodgeThreat.Ranged : Contains(name, MeleeWords) ? DodgeThreat.Melee : DodgeThreat.None;
        }

        private static bool Contains(string name, string[] words)
        {
            foreach (var word in words)
            {
                if (name.Contains(word, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

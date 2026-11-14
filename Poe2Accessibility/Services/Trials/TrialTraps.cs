namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>A kind of trap of the Trials.</summary>
    public enum TrialTrap
    {
        Spikes,
        Laser,
        Mine,
        Turret,
        Quicksand,
        PressurePlate,
        TimeTrap,
        ProjectilePortal,
        PetrifyingStatue,
        StormRune,
        BloodMist,
    }

    /// <summary>
    ///     The Trials' traps by their files: which trap an object is, whether a sighted player sees it before it strikes, and
    ///     which state of its StateMachine means it is striking (.ot files, docs/research/endgame-ui.md). Pure.
    /// </summary>
    public static class TrialTraps
    {
        private const string SanctumHazards = "Metadata/Monsters/MarakethSanctumTrial/Hazards/";
        private const string UltimatumHazards = "Metadata/Monsters/LeagueUltimatum/";
        private const char InstanceMark = '@';

        private static readonly Dictionary<string, (TrialTrap Kind, string[] Firing)> Files = new(StringComparer.Ordinal)
        {
            [SanctumHazards + "GroundSpike"] = (TrialTrap.Spikes, new[] { "rumble", "spike" }),
            [SanctumHazards + "LaserCrossBeamTurret"] = (TrialTrap.Laser, new[] { "fire" }),
            [SanctumHazards + "ProxMine"] = (TrialTrap.Mine, new[] { "prime" }),
            [SanctumHazards + "BulletHellTurret"] = (TrialTrap.Turret, Array.Empty<string>()),
            [SanctumHazards + "QuicksandPit"] = (TrialTrap.Quicksand, Array.Empty<string>()),
            [SanctumHazards + "PressurePlate_DartLauncher"] = (TrialTrap.PressurePlate, Array.Empty<string>()),
            [SanctumHazards + "PressurePlate_PoisonGas"] = (TrialTrap.PressurePlate, Array.Empty<string>()),
            [SanctumHazards + "TimeRewindTrap"] = (TrialTrap.TimeTrap, new[] { "active" }),
            [SanctumHazards + "ProjectilePortal"] = (TrialTrap.ProjectilePortal, Array.Empty<string>()),
        };

        /// <summary>
        ///     The hazards of the Trial of Chaos are monsters, spawned under their MonsterVarieties Id, which no script file is
        ///     named after (docs/research/trial-of-chaos.md): a rune warns while it charges, the others strike without a state.
        /// </summary>
        private static readonly Dictionary<string, (TrialTrap Kind, string[] Firing)> Varieties = new(StringComparer.Ordinal)
        {
            [UltimatumHazards + "Turrets/FireTurret1"] = (TrialTrap.Turret, Array.Empty<string>()),
            [UltimatumHazards + "Turrets/LightningTurret1"] = (TrialTrap.Turret, Array.Empty<string>()),
            [UltimatumHazards + "Turrets/LightningTurret2"] = (TrialTrap.Turret, Array.Empty<string>()),
            [UltimatumHazards + "Turrets/PetrificationStatue1"] = (TrialTrap.PetrifyingStatue, Array.Empty<string>()),
            [UltimatumHazards + "Turrets/PetrificationStatue2"] = (TrialTrap.PetrifyingStatue, Array.Empty<string>()),
            [UltimatumHazards + "Daemon/RotatingLaserPyramid"] = (TrialTrap.Laser, Array.Empty<string>()),
            [UltimatumHazards + "Daemon/RotatingLaserPyramid2"] = (TrialTrap.Laser, Array.Empty<string>()),
            [UltimatumHazards + "Daemon/StormcallerRune"] = (TrialTrap.StormRune, new[] { "charging" }),
            [UltimatumHazards + "Daemon/BloodFog"] = (TrialTrap.BloodMist, Array.Empty<string>()),
        };

        /// <summary>Each trap's file and the states matched as its strike, for the data step's check (<see cref="GameNames"/>).</summary>
        public static IEnumerable<(string File, IReadOnlyList<string> Firing)> FilesMatched => Files.Select(pair => (pair.Key, (IReadOnlyList<string>)pair.Value.Firing));

        /// <summary>The MonsterVarieties Ids matched, by their last segment, for the same check.</summary>
        public static IEnumerable<string> VarietiesMatched => Varieties.Keys.Select(id => id[(id.LastIndexOf('/') + 1)..]);

        /// <summary>The trap an object's path stands for; null for anything else, the spawners and controllers behind them too.</summary>
        public static TrialTrap? KindOf(string path) => Entry(path)?.Kind;

        /// <summary>Spikes lie hidden in the floor: only the ground's rumble gives them away.</summary>
        public static bool IsSeenBeforeItStrikes(TrialTrap kind) => kind != TrialTrap.Spikes;

        /// <summary>Whether the trap at this path is striking or about to, by the values of its states.</summary>
        public static bool IsFiring(string path, IEnumerable<(string Name, long Value)> states) =>
            Entry(path) is { } entry && states.Any(state => state.Value > 0 && entry.Firing.Contains(state.Name, StringComparer.Ordinal));

        private static (TrialTrap Kind, string[] Firing)? Entry(string path)
        {
            var mark = path.IndexOf(InstanceMark);
            var file = mark < 0 ? path : path[..mark];
            return Files.TryGetValue(file, out var entry) || Varieties.TryGetValue(file, out entry) ? entry : null;
        }
    }

    /// <summary>
    ///     Which striking traps are news: each once per strike, and only once the player is within its reach. One that began
    ///     to strike farther off is still news when the player walks up to it (a laser that fires on; taken as said from
    ///     afar, it never warned). Pure.
    /// </summary>
    public sealed class TrapStrikes
    {
        private readonly HashSet<uint> warned = new();

        /// <summary>A new area: no trap is known to be striking.</summary>
        public void Reset() => this.warned.Clear();

        /// <summary>One look at a trap; true when its strike is to be warned of now.</summary>
        public bool Update(uint id, bool firing, bool inReach)
        {
            if (!firing)
            {
                this.warned.Remove(id);
                return false;
            }

            return inReach && this.warned.Add(id);
        }
    }
}

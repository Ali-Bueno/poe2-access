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
            [SanctumHazards + "BulletHellSpinner"] = (TrialTrap.Turret, Array.Empty<string>()),
            [SanctumHazards + "QuicksandPit"] = (TrialTrap.Quicksand, Array.Empty<string>()),
            [SanctumHazards + "PressurePlate_DartLauncher"] = (TrialTrap.PressurePlate, Array.Empty<string>()),
            [SanctumHazards + "PressurePlate_PoisonGas"] = (TrialTrap.PressurePlate, Array.Empty<string>()),
            [SanctumHazards + "TimeRewindTrap"] = (TrialTrap.TimeTrap, new[] { "active" }),
            [SanctumHazards + "ProjectilePortal"] = (TrialTrap.ProjectilePortal, Array.Empty<string>()),
            [UltimatumHazards + "Turrets/ProjectileTurret"] = (TrialTrap.Turret, Array.Empty<string>()),
            [UltimatumHazards + "Turrets/PetrificationStatue"] = (TrialTrap.PetrifyingStatue, Array.Empty<string>()),
            [UltimatumHazards + "Daemon/LaserPyramid"] = (TrialTrap.Laser, Array.Empty<string>()),
        };

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
            return Files.TryGetValue(mark < 0 ? path : path[..mark], out var entry) ? entry : null;
        }
    }
}

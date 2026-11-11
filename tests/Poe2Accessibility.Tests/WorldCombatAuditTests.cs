namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>Rules of the scanner, the guide and the combat cues found wanting by the second audit of 2026-11-10.</summary>
    public static class WorldCombatAuditTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ABossSharesItsNameOnlyWithWhatCanBeTargeted), ABossSharesItsNameOnlyWithWhatCanBeTargeted),
            (nameof(ATrapStrikingFromAfarStillWarnsOnceInReach), ATrapStrikingFromAfarStillWarnsOnceInReach),
            (nameof(TheGuideKeepsItsLockThroughARespawn), TheGuideKeepsItsLockThroughARespawn),
            (nameof(ATargetTheScannerNeverListedIsNotLostFromView), ATargetTheScannerNeverListedIsNotLostFromView),
            (nameof(ABossThatRisesAfterItsDefeatIsFollowedAgain), ABossThatRisesAfterItsDefeatIsFollowedAgain),
            (nameof(ASeatsVoicePassesEveryKindOfLineOn), ASeatsVoicePassesEveryKindOfLineOn),
        };

        /// <summary>A boss's next form waits untargetable under its name: the life said is still the fighting one's.</summary>
        private static void ABossSharesItsNameOnlyWithWhatCanBeTargeted()
        {
            const string Boss = "Conde Geonor";
            var fighting = new[] { ((IntPtr)0x100, Boss, 900f) };
            var waiting = new[] { ((IntPtr)0x200, Boss, 100f) };
            Equal(((IntPtr)0x100, true), TargetMatch.Pick(Boss, fighting, waiting), "the one in play is the target, however near the other stands");
            Equal(((IntPtr)0x200, true), TargetMatch.Pick(Boss, Array.Empty<(IntPtr, string, float)>(), waiting), "none in play: the one set aside is all there is");
            var pack = new[] { ((IntPtr)0x300, "Lobo", 400f), ((IntPtr)0x400, "Lobo", 250f) };
            Equal(((IntPtr)0x400, false), TargetMatch.Pick("Lobo", pack, waiting), "a pack in play still shares its name");
            Equal((IntPtr.Zero, false), TargetMatch.Pick(Boss, Array.Empty<(IntPtr, string, float)>(), Array.Empty<(IntPtr, string, float)>()), "no one carries it");
        }

        /// <summary>A laser seen firing from across the room warned no one, and was then taken as said when the player reached it.</summary>
        private static void ATrapStrikingFromAfarStillWarnsOnceInReach()
        {
            var strikes = new TrapStrikes();
            Equal(false, strikes.Update(7, firing: true, inReach: false), "striking out of reach: nothing to dodge yet");
            Equal(true, strikes.Update(7, firing: true, inReach: true), "still striking as the player walks up: warned");
            Equal(false, strikes.Update(7, firing: true, inReach: true), "once per strike");
            Equal(false, strikes.Update(7, firing: true, inReach: false), "stepping back out");
            Equal(false, strikes.Update(7, firing: true, inReach: true), "and in again during the same strike says nothing new");
            Equal(false, strikes.Update(7, firing: false, inReach: true), "it stopped");
            Equal(true, strikes.Update(7, firing: true, inReach: true), "the next strike is news");
            Equal(true, strikes.Update(8, firing: true, inReach: true), "each trap on its own");
            strikes.Reset();
            Equal(true, strikes.Update(7, firing: true, inReach: true), "another area's trap under the same id");
        }

        /// <summary>The hash names the instance; the object the game holds it in may be another after a respawn.</summary>
        private static void TheGuideKeepsItsLockThroughARespawn()
        {
            Equal(true, BeaconMath.SameInstance((IntPtr)0x1000, "AAAA", (IntPtr)0x1000, "AAAA"), "nothing changed");
            Equal(true, BeaconMath.SameInstance((IntPtr)0x1000, "AAAA", (IntPtr)0x2000, "AAAA"), "the same instance loaded again elsewhere");
            Equal(false, BeaconMath.SameInstance((IntPtr)0x1000, "AAAA", (IntPtr)0x1000, "BBBB"), "another instance at the same address");
            Equal(false, BeaconMath.SameInstance((IntPtr)0x1000, "AAAA", (IntPtr)0x2000, "BBBB"), "another instance");
            Equal(false, BeaconMath.SameInstance((IntPtr)0x1000, "AAAA", IntPtr.Zero, string.Empty), "no instance held");
            Equal(true, BeaconMath.SameInstance((IntPtr)0x1000, string.Empty, (IntPtr)0x1000, string.Empty), "locked before the hash was read: the object stands for it");
            Equal(false, BeaconMath.SameInstance((IntPtr)0x1000, string.Empty, (IntPtr)0x1000, "AAAA"), "and a hash that comes later is not known to be its own");
        }

        /// <summary>The Trial's room door is guided to and never in the scanner's list: "no longer in sight" was said as the player came near it.</summary>
        private static void ATargetTheScannerNeverListedIsNotLostFromView()
        {
            const float Near = 600f;
            var door = new ScanEntry(7, ScanCategory.Exits, "Trial door", 0, 0, 0);
            Equal(GoneTarget.Unknown, BeaconMath.Gone(door, true, 300f, Near, listed: false), "never listed: not being listed says nothing of it");
            Equal(GoneTarget.LostFromView, BeaconMath.Gone(door, true, 300f, Near, listed: true), "listed once and no more: it left");
            Equal(GoneTarget.Unknown, BeaconMath.Gone(new ScanEntry(8, ScanCategory.Chests, "Chest", 0, 0, 0), true, 300f, Near, listed: false), "nor is an unlisted chest opened");
        }

        /// <summary>A second life under the same entity: after "defeated" its life was never said again.</summary>
        private static void ABossThatRisesAfterItsDefeatIsFollowedAgain()
        {
            Equal(BossLifeSteps.Full, BossLifeSteps.Alive(0), "said defeated and alive again: its steps start over");
            Equal(40, BossLifeSteps.Alive(40), "one that never fell keeps its step");
            Equal(0, BossLifeSteps.Next(0, 60), "left at 0, no step is ever reached again");
            Equal(75, BossLifeSteps.Next(BossLifeSteps.Alive(0), 60), "risen and hurt to 60 percent: said from 75 down again");
        }

        /// <summary>"Vendido: ..." was asked to be kept and went out as a plain interrupt, cut by the next focus: the seat's voice lacked the call.</summary>
        private static void ASeatsVoicePassesEveryKindOfLineOn()
        {
            var heard = new HeardOutput();
            IAccessibilityOutputService seat = new SeatOutput(heard, () => "Yarkey");
            seat.Keep("Sold: ring", 900, true, yieldsToMoves: true);
            seat.Moved("row 2");
            Equal("keep Yarkey: Sold: ring True True|moved Yarkey: row 2", string.Join("|", heard.Calls), "kept as asked, and a move told as one");

            // Whatever the interface gains later: a call the seat's voice does not make its own falls to the interface's default.
            var map = typeof(SeatOutput).GetInterfaceMap(typeof(IAccessibilityOutputService));
            var missing = map.TargetMethods.Where(method => method.DeclaringType != typeof(SeatOutput)).Select(method => method.Name).ToList();
            Equal(string.Empty, string.Join(", ", missing), "every call of the output is the seat's own");
        }

        private sealed class HeardOutput : IAccessibilityOutputService
        {
            public List<string> Calls { get; } = new();

            public string BackendName => "test";

            public void Speak(string text, bool interrupt = false) => this.Calls.Add($"speak {text} {interrupt}");

            public void Urge(string text, int holdMs = 1500, bool yieldsToMoves = false) => this.Calls.Add($"urge {text}");

            public void Keep(string text, int holdMs, bool interrupt = false, bool yieldsToMoves = false) => this.Calls.Add($"keep {text} {interrupt} {yieldsToMoves}");

            public void Moved(string text) => this.Calls.Add($"moved {text}");

            public void Repeat()
            {
            }

            public void Stop()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}

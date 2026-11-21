namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The ways of the forks walked through that still go on, and a quest item's use named by the game.</summary>
    public static class PendingWayTests
    {
        private const float LeaveCells = 20f;

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(LeavingAForkTellsTheWaysNotTaken), LeavingAForkTellsTheWaysNotTaken),
            (nameof(LeavingByTheWayBackTellsEveryWayStillOn), LeavingByTheWayBackTellsEveryWayStillOn),
            (nameof(AForkIsLeftOnceAndNothingWhileStillThere), AForkIsLeftOnceAndNothingWhileStillThere),
            (nameof(TwoForksCloseTogetherAreEachLeft), TwoForksCloseTogetherAreEachLeft),
            (nameof(AWayExploredIsNoLongerPending), AWayExploredIsNoLongerPending),
            (nameof(TheNearestPendingWayLeavesOutThePlaceStoodIn), TheNearestPendingWayLeavesOutThePlaceStoodIn),
            (nameof(AnObjectNamedAfterACarriedQuestItemTakesIt), AnObjectNamedAfterACarriedQuestItemTakesIt),
            (nameof(PendingLinesReadInSpanish), PendingLinesReadInSpanish),
        };

        /// <summary>A way out of a fork at the origin along <paramref name="direction"/>, its seen-check cell 30 cells along.</summary>
        private static Way Out(int edge, Vector2 direction) => new(edge, edge + 10, Vector2.Normalize(direction), Vector2.Normalize(direction) * 30, false);

        private static void LeavingAForkTellsTheWaysNotTaken()
        {
            var pending = new PendingWays(LeaveCells);
            pending.Note(1, "Zona 5", Vector2.Zero, 10, new[] { Out(1, Vector2.UnitX), Out(2, -Vector2.UnitY) });
            var left = pending.Leave(new Vector2(40, 2));
            Equal(true, left is { Count: 1 } && left[0].Way.Edge == 2, "the way walked along is not told, the other is");
            Equal(2, pending.Count, "the way taken stays pending until explored");
        }

        private static void LeavingByTheWayBackTellsEveryWayStillOn()
        {
            var pending = new PendingWays(LeaveCells);
            pending.Note(1, "Zona 5", Vector2.Zero, 10, new[] { Out(1, Vector2.UnitX), Out(2, -Vector2.UnitY) });
            Equal(2, pending.Leave(new Vector2(-40, 0))?.Count ?? 0, "turned back: both ways are left pending");
        }

        private static void AForkIsLeftOnceAndNothingWhileStillThere()
        {
            var pending = new PendingWays(LeaveCells);
            pending.Note(1, "Cruce 3", Vector2.Zero, 10, new[] { Out(1, Vector2.UnitX), Out(2, Vector2.UnitY) });
            Equal(true, pending.Leave(new Vector2(25, 0)) == null, "within the fork's radius and the slack: not left yet");
            Equal(true, pending.Leave(new Vector2(0, -40)) != null, "left");
            Equal(true, pending.Leave(new Vector2(0, -80)) == null, "told once");

            var walked = new PendingWays(LeaveCells);
            walked.Note(2, "Cruce 4", Vector2.Zero, 10, new[] { Out(3, Vector2.UnitX) });
            Equal(true, walked.Leave(new Vector2(40, 0)) == null, "the only way on was the one taken: nothing to tell");
        }

        private static void TwoForksCloseTogetherAreEachLeft()
        {
            var pending = new PendingWays(LeaveCells);
            pending.Note(1, "Cruce 1", Vector2.Zero, 10, new[] { Out(1, Vector2.UnitY), Out(2, Vector2.UnitX) });
            pending.Note(2, "Cruce 2", new Vector2(25, 0), 10, new[] { Out(3, Vector2.UnitY), Out(4, Vector2.UnitX) });
            var first = pending.Leave(new Vector2(70, 0));
            var second = pending.Leave(new Vector2(70, 0));
            Equal(true, first != null && second != null && first[0].Node != second[0].Node, "the second fork does not hide the first");
        }

        private static void AWayExploredIsNoLongerPending()
        {
            var pending = new PendingWays(LeaveCells);
            pending.Note(1, "Zona 5", Vector2.Zero, 10, new[] { Out(1, Vector2.UnitX), Out(2, -Vector2.UnitY) });
            pending.Drop(way => way.Way.Edge == 1);
            Equal(true, pending.All.Count == 1 && pending.All[0].Way.Edge == 2, "only what still goes on is left");
            pending.Note(1, "Zona 5", Vector2.Zero, 10, Array.Empty<Way>());
            Equal(0, pending.Count, "coming back to the fork with nothing going on clears it");
        }

        private static void TheNearestPendingWayLeavesOutThePlaceStoodIn()
        {
            var pending = new PendingWays(LeaveCells);
            pending.Note(1, "Zona 1", Vector2.Zero, 10, new[] { Out(1, Vector2.UnitX) });
            pending.Note(2, "Zona 2", new Vector2(500, 0), 10, new[] { Out(2, Vector2.UnitX) });
            Equal("Zona 1", pending.Nearest(new Vector2(100, 0))?.Place, "nearest by the way's start");
            Equal("Zona 2", pending.Nearest(new Vector2(100, 0), exceptNode: 1)?.Place, "not the place the player stands in");
            Equal(true, new PendingWays(LeaveCells).Nearest(Vector2.Zero) == null, "nothing pending");
        }

        private static void AnObjectNamedAfterACarriedQuestItemTakesIt()
        {
            var carried = new[] { "Corazón sacrificial", "Ídolo elegante" };
            Equal(true, KeyPedestals.TakesCarried("Colocar corazón sacrificial", carried), "Aggorat's altar, live 2026-11-21");
            Equal(false, KeyPedestals.TakesCarried("Corazón sacrificial", carried), "the item itself on the ground is no place to use it");
            Equal(false, KeyPedestals.TakesCarried("Altar de ritual", carried), "an object naming no carried item");
            Equal(false, KeyPedestals.TakesCarried("Colocar corazón sacrificial", Array.Empty<string>()), "nothing carried");
        }

        private static void PendingLinesReadInSpanish()
        {
            Localization.Set("es", new());
            try
            {
                Expect("Dejas un camino pendiente a la izquierda", Announcements.PendingLeft(1, new[] { "a la izquierda" }), "one way");
                Expect("Dejas 2 caminos pendientes: arriba y a la izquierda", Announcements.PendingLeft(2, new[] { "arriba", "a la izquierda" }), "two ways");
                Expect("Antes de salir: 2 caminos pendientes", Announcements.PendingBeforeExit(2), "at an exit");
                Expect("Camino pendiente en Zona 5", Announcements.PendingWayAt("Zona 5"), "the scanner's name");
            }
            finally
            {
                Localization.Set("en", new());
            }
        }
    }
}

namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>Ground of one area the player is carried between: a tomb and the land around it (docs/research/map-pieces.md).</summary>
    public static class GroundPiecesTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ATombCarriedToHidesTheLandOutside), ATombCarriedToHidesTheLandOutside),
            (nameof(GroundAGateCutsOffIsNeverHidden), GroundAGateCutsOffIsNeverHidden),
            (nameof(TheWayBetweenTwoGroundsIsWhereTheCarriesWere), TheWayBetweenTwoGroundsIsWhereTheCarriesWere),
            (nameof(WalkingSetsNothingApart), WalkingSetsNothingApart),
            (nameof(ATombsWayBackSetsItApartBeforeAnyCarry), ATombsWayBackSetsItApartBeforeAnyCarry),
        };

        private static readonly Vector2 Outside = new(50, 50);
        private static readonly Vector2 Stairs = new(90, 50);
        private static readonly Vector2 InTomb = new(150, 50);
        private static readonly Vector2 BehindGate = new(250, 50);

        /// <summary>Main land below x 100, the tomb to 200, ground behind a closed gate past it; nothing at negative x.</summary>
        private static int PieceAt(Vector2 cell) => cell.X < 0 ? 0 : cell.X < 100 ? 1 : cell.X < 200 ? 2 : 3;

        private static GroundPieces InTheTomb()
        {
            var pieces = new GroundPieces();
            pieces.Update(Outside, null, PieceAt);
            Equal(PieceNews.EnteredApart, pieces.Update(InTomb, Stairs, PieceAt), "down the stairs: ground apart");
            return pieces;
        }

        private static void ATombCarriedToHidesTheLandOutside()
        {
            var pieces = InTheTomb();
            Equal(true, pieces.InPieceApart(InTomb, PieceAt), "in the tomb");
            Equal(true, pieces.Hides(Outside, InTomb, PieceAt), "the land outside is not told");
            Equal(true, pieces.Hides(BehindGate, InTomb, PieceAt), "nor anything else off the tomb");
            Equal(false, pieces.Hides(InTomb + new Vector2(10, 0), InTomb, PieceAt), "the tomb's own ground is");
            Equal(false, pieces.Hides(new Vector2(-5, 0), InTomb, PieceAt), "ground not known is never hidden");
        }

        private static void GroundAGateCutsOffIsNeverHidden()
        {
            var pieces = InTheTomb();
            Equal(PieceNews.BackToMain, pieces.Update(Stairs, InTomb, PieceAt), "up the stairs: back on the main ground");
            Equal(false, pieces.InPieceApart(Stairs, PieceAt), "outside");
            Equal(true, pieces.Hides(InTomb, Outside, PieceAt), "the tomb is not told from outside");
            Equal(false, pieces.Hides(BehindGate, Outside, PieceAt), "ground behind a closed gate is");
        }

        private static void TheWayBetweenTwoGroundsIsWhereTheCarriesWere()
        {
            var pieces = InTheTomb();
            Equal(InTomb, pieces.WayToward(Outside, InTomb + new Vector2(20, 0), PieceAt), "out of the tomb: where the stairs arrived, by its way back");
            Equal(Stairs, pieces.WayToward(InTomb, Outside, PieceAt), "into it again: where the player went down");
            Equal(null, pieces.WayToward(BehindGate, Outside, PieceAt), "no carry joined that ground");
        }

        private static void WalkingSetsNothingApart()
        {
            var pieces = new GroundPieces();
            pieces.Update(Outside, null, PieceAt);
            Equal(PieceNews.None, pieces.Update(InTomb, null, PieceAt), "a walk, however it got there");
            Equal(false, pieces.AnyApart, "nothing apart");
            Equal(false, pieces.Hides(Outside, InTomb, PieceAt), "nothing hidden");
        }

        private static void ATombsWayBackSetsItApartBeforeAnyCarry()
        {
            var pieces = new GroundPieces();
            pieces.MarkApart(InTomb, PieceAt);
            Equal(false, pieces.AnyApart, "the main ground not known yet: nothing to set apart from");
            pieces.Update(Outside, null, PieceAt);
            pieces.MarkApart(Outside + new Vector2(5, 0), PieceAt);
            Equal(false, pieces.AnyApart, "a way named after the area on the main ground sets nothing apart");
            pieces.MarkApart(InTomb, PieceAt);
            Equal(true, pieces.Hides(InTomb + new Vector2(10, 0), Outside, PieceAt), "the tomb is not told from outside, never entered");
            Equal(false, pieces.Hides(BehindGate, Outside, PieceAt), "ground behind a gate still is");
        }
    }
}

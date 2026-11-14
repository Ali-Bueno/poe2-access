namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The walkable grid for routes: walkable only where every recent sample was, and open again once they agree.</summary>
    public static class GridSamplesTests
    {
        private const int MaxSamples = 4;
        private const int SampleMs = 500;

        /// <summary>One row of 100 bytes (200 cells); the door is the two cells of this byte.</summary>
        private const int Row = 100;
        private const int Door = 90;

        /// <summary>The player by the door, where an open cell is believed, and too far from it for that.</summary>
        private static readonly (int X, int Y)? Near = (Door * 2, 0);
        private static readonly (int X, int Y)? Far = (0, 0);

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ADoorThatOpensIsOpenOnceFourSamplesAgree), ADoorThatOpensIsOpenOnceFourSamplesAgree),
            (nameof(AFlickeringBarrierStaysClosed), AFlickeringBarrierStaysClosed),
            (nameof(ABarrierTheMemoryForgetsOpensThoughTheSamplesAreTheSame), ABarrierTheMemoryForgetsOpensThoughTheSamplesAreTheSame),
            (nameof(AGridOfAnotherLengthStartsOver), AGridOfAnotherLengthStartsOver),
            (nameof(AnOpenedBarrierTakesEffectOnTheNextCall), AnOpenedBarrierTakesEffectOnTheNextCall),
            (nameof(ASampleKeptOwnsItsArray), ASampleKeptOwnsItsArray),
            (nameof(OneSampleAnswersUntilTheNextIsDue), OneSampleAnswersUntilTheNextIsDue),
            (nameof(TheChangedRowsGiveWhatEveryByteGives), TheChangedRowsGiveWhatEveryByteGives),
        };

        /// <summary>
        ///     Reading only the rows that changed and the player's square gives what reading every byte gives: random grids
        ///     whose walls rise and fall by the player and far from them, the player moving, still or unknown.
        /// </summary>
        private static void TheChangedRowsGiveWhatEveryByteGives()
        {
            // 322 x 401 cells: more than twice the trusted square each way, and a length that is not a multiple of eight.
            const int row = 161, rows = 401, steps = 120;
            for (var seed = 1; seed <= 4; seed++)
            {
                var random = new Random(seed);
                var loaded = new byte[row * rows];
                random.NextBytes(loaded);
                var live = (byte[])loaded.Clone();
                var samples = new GridSamples(MaxSamples, SampleMs);
                var reference = new EveryByte(loaded, row);
                (int X, int Y)? player = (row, rows / 2);
                long now = 0;
                for (var step = 0; step < steps; step++)
                {
                    var change = random.Next(4);
                    if (change > 0)
                    {
                        // A wall by the player (or anywhere when unknown), one far from them, and stray cells.
                        var (x, y) = player ?? (random.Next(row * 2), random.Next(rows));
                        Wall(live, row, x + random.Next(-8, 9), y + random.Next(-8, 9), random);
                        Wall(live, row, (x + row) % (row * 2), (y + (rows / 2)) % rows, random);
                        for (var i = 0; i < change * 3; i++)
                        {
                            live[random.Next(live.Length)] = (byte)random.Next(256);
                        }
                    }

                    var move = random.Next(6);
                    player = move == 0 ? null : move <= 2 ? (random.Next(row * 2), random.Next(rows)) : player ?? (row, rows / 2);
                    if (random.Next(20) == 0)
                    {
                        samples.Forget();
                        reference.Forget();
                    }

                    var grid = samples.Add(now += SampleMs, live, loaded, row, player);
                    var expected = reference.Add(live, player);
                    Expect(true, grid.AsSpan().SequenceEqual(expected), $"seed {seed}, sample {step}: the same grid");
                    Expect(reference.Memory.ClosedCount, samples.ClosedCells, $"seed {seed}, sample {step}: the same cells remembered closed");
                }
            }
        }

        /// <summary>A wall of 6 x 3 cells at (x, y) shut, or opened to random ground.</summary>
        private static void Wall(byte[] grid, int row, int x, int y, Random random)
        {
            var shut = random.Next(2) == 0;
            for (var cy = Math.Max(0, y); cy < Math.Min(y + 3, grid.Length / row); cy++)
            {
                for (var cx = Math.Max(0, x); cx < Math.Min(x + 6, row * 2); cx++)
                {
                    var mask = (cx & 1) == 0 ? 0x0F : 0xF0;
                    var value = shut ? 0 : random.Next(1, 16) * ((cx & 1) == 0 ? 0x01 : 0x10);
                    grid[(cy * row) + (cx / 2)] = (byte)((grid[(cy * row) + (cx / 2)] & ~mask) | value);
                }
            }
        }

        /// <summary>The samples and the barrier memory as they were before rows were compared: every byte of every sample read.</summary>
        private sealed class EveryByte
        {
            private readonly Queue<byte[]> kept = new();
            private readonly byte[] loaded;
            private readonly int row;

            public EveryByte(byte[] loaded, int row) => (this.loaded, this.row) = (loaded, row);

            public BarrierMemory Memory { get; } = new();

            public void Forget() => this.kept.Clear();

            public byte[] Add(byte[] live, (int X, int Y)? player)
            {
                this.kept.Enqueue((byte[])live.Clone());
                while (this.kept.Count > MaxSamples)
                {
                    this.kept.Dequeue();
                }

                if (player is { } at)
                {
                    this.Memory.Observe(live, this.loaded, this.row, at.X, at.Y);
                }

                var grid = (byte[])live.Clone();
                foreach (var sample in this.kept)
                {
                    for (var i = 0; i < grid.Length; i++)
                    {
                        grid[i] &= (byte)(((sample[i] & 0x0F) == 0 ? 0xF0 : 0xFF) & ((sample[i] & 0xF0) == 0 ? 0x0F : 0xFF));
                    }
                }

                this.Memory.Apply(grid);
                return grid;
            }
        }

        private static void ADoorThatOpensIsOpenOnceFourSamplesAgree()
        {
            var samples = new GridSamples(MaxSamples, SampleMs);
            var closed = Grid(doorOpen: false);
            var open = Grid(doorOpen: true);
            long now = 0;
            for (var sample = 0; sample < 4; sample++)
            {
                Expect(false, IsOpen(samples.Add(now += SampleMs, closed, closed, Row, Near)), $"closed sample {sample}");
            }

            // The door opens: closed while any sample still saw it closed, open with the fourth that agrees (2 s), and for good.
            var opened = now;
            for (var sample = 1; sample <= 3; sample++)
            {
                Expect(false, IsOpen(samples.Add(now += SampleMs, open, closed, Row, Near)), $"open sample {sample} of four");
            }

            var agreed = samples.Add(now += SampleMs, open, closed, Row, Near);
            Expect(true, IsOpen(agreed), "the fourth open sample");
            Expect(true, now - opened <= 4 * SampleMs, "open within two seconds");
            for (var sample = 0; sample < 6; sample++)
            {
                var later = samples.Add(now += SampleMs, open, closed, Row, Near);
                Expect(true, IsOpen(later), $"still open {sample} samples later");
                Expect(true, ReferenceEquals(agreed, later), "and the same array while nothing changes");
            }

            // And it closes at once: one sample that sees it closed is enough.
            Expect(false, IsOpen(samples.Add(now += SampleMs, closed, closed, Row, Near)), "the door closed again");
        }

        private static void AFlickeringBarrierStaysClosed()
        {
            var samples = new GridSamples(MaxSamples, SampleMs);
            var closed = Grid(doorOpen: false);
            var open = Grid(doorOpen: true);
            long now = 0;
            for (var sample = 0; sample < 12; sample++)
            {
                // Open three samples in four: never four in a row.
                var live = sample % 4 == 0 ? closed : open;
                Expect(false, IsOpen(samples.Add(now += SampleMs, live, closed, Row, Near)), $"sample {sample}");
            }
        }

        private static void ABarrierTheMemoryForgetsOpensThoughTheSamplesAreTheSame()
        {
            var samples = new GridSamples(MaxSamples, SampleMs);
            var loaded = Grid(doorOpen: true);
            var closed = Grid(doorOpen: false);
            long now = 0;

            // Walkable as loaded, then closed: the memory holds it closed, and an open reading from afar is not believed.
            samples.Add(now += SampleMs, closed, loaded, Row, Far);
            Expect(2, samples.ClosedCells, "the door's two cells remembered closed");
            byte[] held = closed;
            for (var sample = 0; sample < 6; sample++)
            {
                held = samples.Add(now += SampleMs, loaded, loaded, Row, Far);
                Expect(false, IsOpen(held), $"open from afar, sample {sample}");
            }

            // The player walks up: the same samples, but the memory lets go, so the grid kept is made again.
            var near = samples.Add(now += SampleMs, loaded, loaded, Row, Near);
            Expect(0, samples.ClosedCells, "forgotten once it reads open near the player");
            Expect(true, IsOpen(near), "open though every sample is the array it was");
            Expect(false, ReferenceEquals(held, near), "the grid kept before was not handed out again");
        }

        private static void AGridOfAnotherLengthStartsOver()
        {
            var samples = new GridSamples(MaxSamples, SampleMs);
            var closed = Grid(doorOpen: false);
            long now = 0;
            for (var sample = 0; sample < 4; sample++)
            {
                samples.Add(now += SampleMs, closed, closed, Row, Near);
            }

            // Another area's grid: nothing of the old samples goes into it, and the old grid answers for no size.
            var other = new byte[Row * 2];
            Array.Fill(other, (byte)0x11);
            var grid = samples.Add(now += SampleMs, other, other, Row, Near);
            Expect(other.Length, grid.Length, "the new grid's length");
            Expect(true, grid.AsSpan().SequenceEqual(other), "as the game holds it, from its first sample");
            Expect(true, samples.Fresh(now, closed.Length) == null, "the old size has no answer");
            Expect(true, ReferenceEquals(grid, samples.Fresh(now, other.Length)), "the new one has");
        }

        private static void AnOpenedBarrierTakesEffectOnTheNextCall()
        {
            var samples = new GridSamples(MaxSamples, SampleMs);
            var closed = Grid(doorOpen: false);
            var open = Grid(doorOpen: true);
            long now = 0;
            for (var sample = 0; sample < 4; sample++)
            {
                samples.Add(now += SampleMs, closed, closed, Row, Near);
            }

            Expect(false, samples.Fresh(now + 1, closed.Length) == null, "a sample just taken answers");
            samples.Forget();
            Expect(true, samples.Fresh(now + 1, closed.Length) == null, "forgotten: the next call samples, however soon");
            Expect(true, IsOpen(samples.Add(now + 1, open, closed, Row, Near)), "and reads the door open at once");
        }

        private static void ASampleKeptOwnsItsArray()
        {
            var samples = new GridSamples(MaxSamples, SampleMs);
            var loaded = Grid(doorOpen: false);
            var buffer = Grid(doorOpen: false); // the caller reads every sample into this one array
            long now = 0;
            for (var sample = 0; sample < 4; sample++)
            {
                samples.Add(now += SampleMs, buffer, loaded, Row, Near);
            }

            // The next read lands in the same buffer: the three samples before it must still say closed.
            buffer[Door] = 0x11;
            for (var sample = 1; sample <= 3; sample++)
            {
                Expect(false, IsOpen(samples.Add(now += SampleMs, buffer, loaded, Row, Near)), $"open sample {sample} of four in the reused buffer");
            }

            var grid = samples.Add(now += SampleMs, buffer, loaded, Row, Near);
            Expect(true, IsOpen(grid), "open with the fourth");
            Expect(false, ReferenceEquals(grid, buffer), "the grid handed out is never the caller's buffer");
            buffer[Door] = 0;
            Expect(true, IsOpen(samples.Fresh(now, buffer.Length)!), "and does not change with it");
        }

        private static void OneSampleAnswersUntilTheNextIsDue()
        {
            var samples = new GridSamples(MaxSamples, SampleMs);
            var closed = Grid(doorOpen: false);
            Expect(true, samples.Fresh(0, closed.Length) == null, "nothing before the first sample");
            var grid = samples.Add(1000, closed, closed, Row, null);
            Expect(true, ReferenceEquals(grid, samples.Fresh(1000 + SampleMs - 1, closed.Length)), "the same grid within a sample's time");
            Expect(true, samples.Fresh(1000 + SampleMs, closed.Length) == null, "a sample is due after it");
            Expect(0, samples.ClosedCells, "no player, nothing learnt of barriers");
        }

        private static byte[] Grid(bool doorOpen)
        {
            var grid = new byte[Row];
            Array.Fill(grid, (byte)0x11);
            grid[Door] = doorOpen ? (byte)0x11 : (byte)0;
            return grid;
        }

        private static bool IsOpen(byte[] grid) => grid[Door] == 0x11;
    }
}

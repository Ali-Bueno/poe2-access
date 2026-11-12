namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader.RemoteEnums;
    using Poe2Accessibility.Models;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>Enemy and radar cues, the mixer, area memory, passage scans and co-op player streams.</summary>
    public static class CueAndRadarSoundTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(EnemyCuesPulseFasterWhenNearAndTakeTurns), EnemyCuesPulseFasterWhenNearAndTakeTurns),
            (nameof(EnemyCueSoundsStayInRangeAndDiffer), EnemyCueSoundsStayInRangeAndDiffer),
            (nameof(CuesRingOutAndTheMixerRoundsOff), CuesRingOutAndTheMixerRoundsOff),
            (nameof(LoudCuesAreLimitedNotSaturated), LoudCuesAreLimitedNotSaturated),
            (nameof(AreaMemoryKeepsTheSameMapOnly), AreaMemoryKeepsTheSameMapOnly),
            (nameof(AreaMemoryCarriesAFixedMapsExits), AreaMemoryCarriesAFixedMapsExits),
            (nameof(PassageScanMeasuresTheWayAhead), PassageScanMeasuresTheWayAhead),
            (nameof(RadarSoundsConfirmedChangesOnly), RadarSoundsConfirmedChangesOnly),
            (nameof(RadarSoundsStayInRangeAndDiffer), RadarSoundsStayInRangeAndDiffer),
            (nameof(PartnerRadarHasItsOwnSounds), PartnerRadarHasItsOwnSounds),
            (nameof(CoopDownsSayWhoFellAndRose), CoopDownsSayWhoFellAndRose),
            (nameof(InputSeatFollowsWhoPressedLast), InputSeatFollowsWhoPressedLast),
            (nameof(LocalPlayerListTrustsOnlyItsOwnPlayerFirst), LocalPlayerListTrustsOnlyItsOwnPlayerFirst),
            (nameof(NearbyThingsAreNamedOncePerApproach), NearbyThingsAreNamedOncePerApproach),
        };

        private static void EnemyCuesPulseFasterWhenNearAndTakeTurns()
        {
            var scheduler = new EnemyCueScheduler(4, 100, 150, 600);
            var near = new CueTarget(1, CueSynth.Layer.None, 0.1f, 0f, 1f);
            var far = new CueTarget(2, CueSynth.Layer.None, -0.9f, 0f, 0f);
            List<uint> Run(IReadOnlyList<CueTarget> targets, long fromMs, long toMs)
            {
                var played = new List<uint>();
                for (var now = fromMs; now < toMs; now += 10)
                {
                    if (scheduler.Next(now, targets) is { } target)
                    {
                        played.Add(target.Id);
                    }
                }

                return played;
            }

            var alone = Run(new[] { near }, 0, 1000);
            Equal(7, alone.Count, "contact pulses every 150 ms in a second");
            scheduler.Reset();
            var both = Run(new[] { near, far }, 0, 1200);
            Equal(true, both.Count(id => id == 1) > both.Count(id => id == 2) * 2, "the near one pulses much more");
            Equal(true, both.Contains(2u), "the far one still gets turns");

            scheduler.Reset();
            var boss = new CueTarget(3, CueSynth.Layer.Boss, 0.5f, -0.5f, 0.2f);
            var crowd = Enumerable.Range(10, 8).Select(id => new CueTarget((uint)id, CueSynth.Layer.None, 0f, 0.1f, 0.9f)).ToArray();
            var mixed = Run(crowd.Append(boss).ToArray(), 0, 1000);
            Equal(true, mixed.Contains(3u), "a far boss is heard among a near crowd");
            Equal(4, mixed.Distinct().Count(), "four streams at most");

            scheduler.Reset();
            Run(new[] { near }, 0, 50);
            var swapped = Run(new[] { near with { Id = 5 } }, 50, 140);
            Equal(0, swapped.Count, "a new nearest enemy keeps the stream's cadence");
        }

        private static void EnemyCueSoundsStayInRangeAndDiffer()
        {
            const int rate = 44100;
            var growl = CueSynth.Enemy(CueSynth.Layer.None, rate);
            var elite = CueSynth.Enemy(CueSynth.Layer.Elite, rate);
            var boss = CueSynth.Enemy(CueSynth.Layer.Boss, rate);
            foreach (var (name, sound) in new[] { ("growl", growl), ("elite", elite), ("boss", boss) })
            {
                Equal(true, sound.Length > rate / 20 && sound.Length < rate / 4, $"{name} lasts 50-250 ms");
                Equal(true, sound.All(sample => MathF.Abs(sample) <= 1f), $"{name} stays within full scale");
                Equal(true, MathF.Abs(sound[^1]) < 0.05f, $"{name} ends near silence");
            }

            int Crossings(float[] sound) => sound.Zip(sound.Skip(1)).Count(pair => pair.First < 0 != pair.Second < 0);
            Equal(true, Crossings(elite) > Crossings(growl) * 2, "the elite layer is brighter than the growl");
            Equal(true, boss.Length > growl.Length, "the boss ring rings past the growl");
        }

        private static void CuesRingOutAndTheMixerRoundsOff()
        {
            const int rate = 44100;
            // The dodge warning's bass drop rings on after the hit, which comes 0.2 s in.
            foreach (var (name, sound, seconds) in new[] { ("beacon", CueSynth.Ping(rate), 0.5f), ("dodge", CueSynth.Dodge(rate), 1f) })
            {
                Equal(true, sound.All(sample => MathF.Abs(sample) <= 1f), $"{name} stays within full scale");
                Equal(true, MathF.Abs(sound[^1]) < 0.01f, $"{name}'s room tail fades to silence");
                Equal(true, sound.Length < rate * seconds, $"{name} is over within {seconds} s");
            }
        }

        private static void LoudCuesAreLimitedNotSaturated()
        {
            const int rate = 44100;
            var tone = Enumerable.Range(0, rate / 2).Select(i => 0.85f * MathF.Sin(2 * MathF.PI * 220 * i / rate)).ToArray();
            var (quiet, _) = PeakLimiter.Drive(tone, tone, 1f, rate);
            Equal(true, quiet.Zip(tone).All(pair => MathF.Abs(pair.First - pair.Second) < 1e-6f), "under the ceiling a cue is untouched");

            // A steady tone at full scale has no quiet body to raise; a cue's decaying body grows.
            foreach (var (name, sound, louder) in new[] { ("tone", tone, 1.1f), ("dodge", CueSynth.Dodge(rate), 1.5f), ("parry", CueSynth.Parry(rate), 1.5f) })
            {
                var (loud, _) = PeakLimiter.Drive(sound, sound, 3f, rate);
                Equal(sound.Length, loud.Length, $"{name} keeps its length at 300%");
                Equal(true, loud.Max(MathF.Abs) <= PeakLimiter.Ceiling + 1e-4f, $"{name} at 300% stays under the ceiling");
                Equal(true, Rms(loud) > Rms(sound) * louder, $"{name} at 300% is louder ({Rms(loud) / Rms(sound):0.00} times)");

                // Limited, not saturated: the output is the input times a gain that glides, never a reshaped wave.
                var gains = sound.Zip(loud).Where(pair => MathF.Abs(pair.First) > 0.05f).Select(pair => pair.Second / (pair.First * 3f)).ToArray();
                Equal(true, gains.All(gain => gain is > 0f and <= 1.0001f), $"{name}: only a gain, same sign");
                var jumps = sound.Select((sample, i) => i > 0 && MathF.Abs(sample) > 0.05f && MathF.Abs(sound[i - 1]) > 0.05f
                    ? MathF.Abs((loud[i] / (sample * 3f)) - (loud[i - 1] / (sound[i - 1] * 3f))) : 0f).Max();
                Equal(true, jumps < 0.02f, $"{name}: the gain glides ({jumps:0.0000} a sample at most)");
            }

            var output = new PeakLimiter(rate);
            var peak = 0f;
            for (var i = 0; i < rate; i++)
            {
                var left = 3f * MathF.Sin(2 * MathF.PI * 110 * i / rate);
                var right = -left;
                output.Process(ref left, ref right);
                peak = MathF.Max(peak, MathF.Max(MathF.Abs(left), MathF.Abs(right)));
            }

            Equal(true, peak <= PeakLimiter.Ceiling + 1e-4f, "a pile-up at the output never passes the ceiling");

            static float Rms(float[] samples) => MathF.Sqrt(samples.Average(sample => sample * sample));
        }

        private static void AreaMemoryKeepsTheSameMapOnly()
        {
            var directory = System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests", "explored-" + Guid.NewGuid().ToString("N"));
            var log = new PluginLog(System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests"), false);
            var tiles = new Dictionary<string, List<Vector2>> { ["forest_a"] = new() { new(23, 0), new(0, 0) }, ["exit"] = new() { new(46, 23) } };
            var shuffled = new Dictionary<string, List<Vector2>> { ["exit"] = new() { new(46, 23) }, ["forest_a"] = new() { new(0, 0), new(23, 0) } };
            var regenerated = new Dictionary<string, List<Vector2>> { ["forest_a"] = new() { new(0, 0), new(23, 23) }, ["exit"] = new() { new(46, 0) } };
            var map = AreaMemory.Layout(tiles, 100, 20000);
            Equal(map, AreaMemory.Layout(shuffled, 100, 20000), "the same tiles in another order are the same map");
            Equal(true, map != AreaMemory.Layout(regenerated, 100, 20000), "other tiles are another map");
            Equal(true, map != AreaMemory.Layout(tiles, 120, 20000), "another grid is another map");

            var exit = new ScanEntry(7, ScanCategory.Exits, "Sierraclara", 1f, 2f, 3f, "exit", Sound: "exit", QuestMarked: true, Subject: "way");
            var snapshot = new AreaMemory.Snapshot(map, new byte[] { 1, 2, 3 }, "Campamento", new() { [7] = exit }, new() { 7, 9 }, new() { [11] = new Vector2(4, 5) });
            var memory = new AreaMemory(directory, log);
            memory.Keep("ABC", snapshot);
            Equal(true, memory.Recall("ABC", map) == snapshot, "back in the same instance: everything returns");

            var restarted = new AreaMemory(directory, log);
            var fromDisk = restarted.Recall("ABC", map);
            Equal(true, fromDisk != null, "after a host restart it returns from disk");
            Equal(exit, fromDisk!.Remembered[7], "with its exits as they were");
            Equal("Campamento", fromDisk.CameFrom, "and the way in");
            Equal(true, fromDisk.Explored.SequenceEqual(snapshot.Explored) && fromDisk.Sighted.SetEquals(snapshot.Sighted), "and the explored ground");
            Equal(new Vector2(4, 5), fromDisk.ClosedBarriers[11], "and the closed barriers");

            Equal(true, restarted.Recall("ABC", AreaMemory.Layout(regenerated, 100, 20000)) == null, "a regenerated map under the same hash starts unexplored");
            Equal(true, new AreaMemory(directory, log).Recall("ABC", map) == null, "and its old file is gone");
            Equal(true, memory.Recall("XYZ", map) == null, "an instance never seen is new");
            System.IO.Directory.Delete(directory, true);
        }

        private static void AreaMemoryCarriesAFixedMapsExits()
        {
            var directory = System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests", "explored-" + Guid.NewGuid().ToString("N"));
            var log = new PluginLog(System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests"), false);
            const string Transition = "Metadata/Terrain/Woods/AreaTransitions/Clearfell_OldForest_Transition_01.tdtx:0-y:0";
            var first = new Dictionary<string, List<Vector2>> { ["Metadata/Terrain/Woods/Stump_A.tdtx:0-y:0"] = new() { new(23, 0) }, [Transition] = new() { new(1311, 552) } };
            var varied = new Dictionary<string, List<Vector2>> { ["Metadata/Terrain/Woods/Stump_B.tdtx:0-y:0"] = new() { new(23, 0) }, [Transition] = new() { new(1311, 552) } };
            var moved = new Dictionary<string, List<Vector2>> { ["Metadata/Terrain/Woods/Stump_A.tdtx:0-y:0"] = new() { new(23, 0) }, [Transition] = new() { new(207, 1173) } };
            var frame = AreaMemory.Frame(first, 100, 20000);
            Equal(true, AreaMemory.Layout(first, 100, 20000) != AreaMemory.Layout(varied, 100, 20000), "Clearfell again: other pieces, another layout");
            Equal(frame, AreaMemory.Frame(varied, 100, 20000), "but its exits where they were: the same frame");
            Equal(true, frame != AreaMemory.Frame(moved, 100, 20000), "a regenerated map moves its exits: another frame");
            Equal(0UL, AreaMemory.Frame(new Dictionary<string, List<Vector2>> { ["Metadata/Terrain/Woods/Stump_A.tdtx:0-y:0"] = new() { new(23, 0) } }, 100, 20000),
                  "a map without transition pieces has no frame");

            var exit = new ScanEntry(7, ScanCategory.Exits, "El bosque singular", 1f, 2f, 3f, "exit");
            var checkpoint = new ScanEntry(8, ScanCategory.Waypoints, "Punto de control", 4f, 5f, 6f, "checkpoint");
            var chest = new ScanEntry(9, ScanCategory.Chests, "Cofre", 7f, 8f, 9f, "chest");
            var death = new ScanEntry(10, ScanCategory.Exits, "Donde moriste", 7f, 8f, 9f);
            var npc = new ScanEntry(11, ScanCategory.Npcs, "Una", 7f, 8f, 9f, "npc");
            var portal = new ScanEntry(12, ScanCategory.Waypoints, "Pueblo de Ogham", 7f, 8f, 9f, ScanEntry.PortalKind);
            Equal(true, portal.CanVanish && chest.CanVanish, "a portal closes as a chest is opened");
            Equal(true, new ScanEntry(13, ScanCategory.PointsOfInterest, "Jaula", 7f, 8f, 9f, MinimapMarks.LandmarkKind, QuestMarked: true).CanVanish, "a quest's object goes once used");
            Equal(false, new ScanEntry(14, ScanCategory.PointsOfInterest, "Pozo", 7f, 8f, 9f, MinimapMarks.LandmarkKind).CanVanish, "a labelled place on the map stays");
            Equal(false, checkpoint.CanVanish || exit.CanVanish || npc.CanVanish, "a checkpoint, an exit and a character stay");
            var left = new AreaMemory.Snapshot(1, new byte[] { 1, 2, 3 }, "Campamento", new() { [7] = exit, [8] = checkpoint, [9] = chest, [10] = death, [11] = npc, [12] = portal },
                                               new() { 7 }, new(), frame);
            new AreaMemory(directory, log).Keep("OLD", left);

            var restarted = new AreaMemory(directory, log);
            Equal(true, restarted.Recall("NEW", AreaMemory.Layout(varied, 100, 20000)) == null, "the new instance itself is unknown");
            var inherited = restarted.Inherit(frame, "NEW");
            Equal("OLD", inherited?.Instance, "but the instance left with the same frame is found on disk");
            Equal("El bosque singular|Punto de control", string.Join("|", AreaMemory.Framed(inherited!.Value.Snapshot).Select(entry => entry.Name)),
                  "and only its exits and checkpoints come along: no chest, death place, character or portal");
            Equal(true, restarted.Inherit(AreaMemory.Frame(moved, 100, 20000), "NEW") == null, "a map whose exits moved inherits nothing");
            Equal(true, restarted.Inherit(frame, "OLD") == null, "nor does the instance itself");
            Equal(true, restarted.Inherit(0, "NEW") == null, "nor a frameless map");
            System.IO.Directory.Delete(directory, true);
        }

        /// <summary>A walkable nibble grid (two cells per byte) with the given cells blocked.</summary>
        private static byte[] Grid(int columns, int rows, params (int X, int Y)[] blocked)
        {
            var grid = Enumerable.Repeat((byte)0x11, columns / 2 * rows).ToArray();
            foreach (var (x, y) in blocked)
            {
                grid[(y * columns / 2) + (x / 2)] &= (byte)((x & 1) == 0 ? 0xF0 : 0x0F);
            }

            return grid;
        }

        private static void PassageScanMeasuresTheWayAhead()
        {
            var start = new System.Numerics.Vector2(20.5f, 50.5f);
            var east = new System.Numerics.Vector2(1, 0);
            var open = PassageScan.Scan(Grid(100, 100), 50, start, east, 2, 40, 20);
            Equal(true, open.Depth >= 39 && !open.Unknown, "open floor runs to the reach");
            var wall = Enumerable.Range(45, 11).Select(y => (30, y)).ToArray();
            var blocked = PassageScan.Scan(Grid(100, 100, wall), 50, start, east, 2, 40, 20);
            Equal(true, blocked.Depth is > 8 and < 10, $"a wall 10 cells ahead stops it ({blocked.Depth})");
            var post = PassageScan.Scan(Grid(100, 100, (30, 50)), 50, start, east, 2, 40, 20);
            Equal(true, post.Depth >= 39, "a one-cell post is walked around inside the lane");
            var edge = PassageScan.Scan(Grid(100, 100), 50, new System.Numerics.Vector2(90.5f, 50.5f), east, 2, 40, 20);
            Equal(true, edge.Unknown && edge.Depth < 20, "the grid's end is unknown, not a wall");
        }

        private static void RadarSoundsConfirmedChangesOnly()
        {
            var sensor = new RadarSensor(new RadarThresholds(20, 15, 3, 200, 100, 500));
            var axis = new System.Numerics.Vector2(1, 0);
            var open = new PassageScan.Result(40, false);
            var wall = new PassageScan.Result(12, false);
            var x = 0f;
            RadarReading? Step(long now, PassageScan.Result scan, float move = 1f, bool exit = false)
            {
                x += move;
                return sensor.Update(now, new System.Numerics.Vector2(x, 0), axis, scan, exit);
            }

            Equal(null, Step(0, open), "the first reading is a silent baseline");
            Equal(null, Step(50, open), "unchanged");
            Equal(null, Step(100, wall), "a wall must be confirmed first");
            Equal(null, Step(150, wall), "one cell walked");
            Equal(null, Step(200, wall), "two cells walked");
            Equal(RadarReading.Wall, Step(250, wall), "confirmed after three cells");
            Equal(null, Step(275, wall), "said once");
            Equal(null, Step(300, new PassageScan.Result(17, false)), "between close and open the wall holds");
            // The wall heard stands at 18; backing away until it is farther than the opening depth reads open.
            foreach (var at in new long[] { 350, 400, 425, 450, 475 })
            {
                x -= 3f;
                Equal(null, sensor.Update(at, new System.Numerics.Vector2(x, 0), axis, new PassageScan.Result(18 - x, false), false), $"backing away to {x} is not an opening");
            }

            Equal(null, Step(500, open, 0f, true), "an exit waits for confirmation");
            Equal(null, Step(600, open, 0f, true), "still standing: not yet");
            Equal(RadarReading.Exit, Step(700, open, 0f, true), "standing still confirms after a moment");
            Equal(null, Step(850, open, 1f), "open again, but the exit heard holds");
            Equal(null, Step(900, open, 1f), "still held");
            Equal(null, Step(950, open, 1f), "still held, and walking on does not release it");
            Equal(null, Step(2250, open, 1f), "the exit has been gone for the hold: open is a candidate");
            Equal(null, Step(2300, open, 1f), "walking on");
            Equal(null, Step(2350, open, 1f), "walking on");
            Equal(RadarReading.Open, Step(2400, open, 1f), "confirmed after three cells, then said");
            sensor.Reset();
            Equal(null, Step(1400, wall), "after a reset the first reading is silent again");
        }

        private static void RadarSoundsStayInRangeAndDiffer()
        {
            const int rate = 44100;
            foreach (var reading in new[] { RadarReading.Open, RadarReading.Exit, RadarReading.Wall })
            {
                foreach (var direction in Enum.GetValues<RadarSynth.Direction>())
                {
                    var sound = RadarSynth.Cue(reading, direction, rate);
                    Equal(true, sound.Length > rate / 20 && sound.Length < rate / 4, $"{reading} {direction} lasts 50-250 ms");
                    Equal(true, sound.All(sample => MathF.Abs(sample) <= 1f), $"{reading} {direction} within full scale");
                }
            }

            int Crossings(float[] sound) => sound.Zip(sound.Skip(1)).Count(pair => pair.First < 0 != pair.Second < 0);
            var exitUp = RadarSynth.Cue(RadarReading.Exit, RadarSynth.Direction.Up, rate);
            var exitDown = RadarSynth.Cue(RadarReading.Exit, RadarSynth.Direction.Down, rate);
            var wallUp = RadarSynth.Cue(RadarReading.Wall, RadarSynth.Direction.Up, rate);
            Equal(true, Crossings(exitDown) < Crossings(exitUp), "below the player sounds lower");
            Equal(true, Crossings(exitUp) > Crossings(wallUp) * 3, "the exit tin is far above the wall tock");
        }

        private static void PartnerRadarHasItsOwnSounds()
        {
            const int rate = 44100;
            foreach (var reading in new[] { RadarReading.Open, RadarReading.Exit, RadarReading.Wall })
            {
                foreach (var direction in Enum.GetValues<RadarSynth.Direction>())
                {
                    var sound = RadarSynth.Cue(reading, direction, rate, RadarSynth.Voice.Second);
                    Equal(true, sound.Length > rate / 20 && sound.Length < rate / 4, $"second voice {reading} {direction} lasts 50-250 ms");
                    Equal(true, sound.All(sample => MathF.Abs(sample) <= 1f), $"second voice {reading} {direction} within full scale");
                }
            }

            float[] Window(float[] sound, int fromMs, int toMs) =>
                sound.Skip(rate * fromMs / 1000).Take(rate * (toMs - fromMs) / 1000).ToArray();
            int[] CrossingsAt(float[] sound) =>
                Enumerable.Range(1, sound.Length - 1).Where(i => sound[i - 1] < 0 != sound[i] < 0).ToArray();
            int Crossings(float[] sound, int fromMs, int toMs) => CrossingsAt(Window(sound, fromMs, toMs)).Length;
            float Loudest(float[] sound, int fromMs, int toMs) => Window(sound, fromMs, toMs).Max(MathF.Abs);
            double Irregularity(float[] sound)
            {
                var at = CrossingsAt(Window(sound, 60, 120));
                var gaps = at.Zip(at.Skip(1), (first, next) => (double)(next - first)).ToArray();
                var mean = gaps.Average();
                return Math.Sqrt(gaps.Average(gap => (gap - mean) * (gap - mean))) / mean;
            }

            double Brightness(float[] sound, int fromMs, int toMs)
            {
                var part = Window(sound, fromMs, toMs);
                return Math.Sqrt(part.Skip(1).Select((sample, i) => (double)(sample - part[i]) * (sample - part[i])).Sum() / part.Sum(sample => (double)sample * sample));
            }

            int Period(float[] sound)
            {
                // the first autocorrelation peak past the first trough: the fundamental, not a multiple of it
                var part = Window(sound, 20, 60);
                var match = Enumerable.Range(0, rate / 80).Select(lag => part.Skip(lag).Select((sample, i) => (double)sample * part[i]).Sum() / (part.Length - lag)).ToArray();
                var trough = Array.FindIndex(match, value => value < 0);
                var lag = Array.FindIndex(match, trough, value => value >= 0.9 * match.Skip(trough).Max());
                while (match[lag + 1] > match[lag])
                {
                    lag++;
                }

                return lag;
            }

            RadarSynth.Direction up = RadarSynth.Direction.Up, down = RadarSynth.Direction.Down, left = RadarSynth.Direction.Left;
            const RadarSynth.Voice second = RadarSynth.Voice.Second;
            var pluck = RadarSynth.Cue(RadarReading.Wall, up, rate, second);
            Equal(true, Loudest(pluck, 70, 90) < Loudest(pluck, 0, 20) / 4, "the wall is plucked once");
            var obstacle = RadarSynth.Obstacle(up, rate, second);
            var exit = RadarSynth.Cue(RadarReading.Exit, up, rate, second);
            Equal(true, Brightness(pluck, 0, 15) > Brightness(pluck, 45, 60) * 1.1, "the pluck starts bright and mellows");
            var tock = RadarSynth.Cue(RadarReading.Wall, up, rate);
            Equal(true, Math.Abs(Brightness(tock, 0, 15) - Brightness(tock, 45, 60)) < Brightness(tock, 0, 15) / 10, "the first voice's tock keeps one tone");
            Equal(true, Brightness(pluck, 0, 15) > Brightness(tock, 0, 15) * 3, "the plucked wall is richer than the first voice's tock, though lower");
            Equal(true, Period(exit) < Period(obstacle) && Period(obstacle) < Period(pluck), "plucked in the first voice's order: exit high, obstacle middle, wall low");
            Equal(true, Math.Abs(Period(pluck) - (rate / 196)) <= 2, "the wall is plucked at 196 Hz");
            Equal(true, Period(RadarSynth.Cue(RadarReading.Exit, down, rate, second)) > Period(exit), "below the player plucks lower");
            var whistle = RadarSynth.Cue(RadarReading.Open, left, rate, second);
            var breath = RadarSynth.Cue(RadarReading.Open, left, rate);
            Equal(breath.Length, whistle.Length, "the whistle lasts as long as the breath");
            Equal(true, Irregularity(whistle) < 0.1, "the second voice whistles a steady tone");
            Equal(true, Irregularity(breath) > 0.2, "the first voice breathes noise");
            var ahead = RadarSynth.Cue(RadarReading.Open, up, rate, second);
            var behind = RadarSynth.Cue(RadarReading.Open, down, rate, second);
            Equal(true, Crossings(ahead, 120, 160) > Crossings(ahead, 20, 60), "the whistle rises ahead");
            Equal(true, Crossings(behind, 120, 160) < Crossings(behind, 20, 60), "the whistle falls behind");
        }

        private static void CoopDownsSayWhoFellAndRose()
        {
            var downs = new CoopDowns();
            const long town = 1, field = 2, respawned = 3;
            Equal(0, downs.Update(field, true, ("yarkbruja", false)).Count, "the first reading (area loaded with the partner dead) is not news");
            Equal(Announcements.PartnerUp("yarkbruja"), downs.Update(field, true, ("yarkbruja", true)).Single(), "revived");
            Equal(Announcements.PartnerDown("yarkbruja"), downs.Update(field, true, ("yarkbruja", false)).Single(), "the partner falls");
            Equal(0, downs.Update(field, true, ("yarkbruja", false)).Count, "still down: said once");
            Equal(0, downs.Update(field, false, ("yarkbruja", false)).Count, "both dead: the death screen speaks");
            Equal(0, downs.Update(respawned, true, ("yarkbruja", true)).Count, "respawned at the checkpoint: a new instance, no news");
            Equal(Announcements.YouAreDown, downs.Update(respawned, false, ("yarkbruja", true)).Single(), "the followed player falls, the partner can revive");
            Equal(Announcements.YouAreUp, downs.Update(respawned, true, ("yarkbruja", true)).Single(), "revived by the partner");
            downs.Update(respawned, false, ("yarkbruja", true));
            downs.Reset();
            Equal(0, downs.Update(town, true, ("yarkbruja", true)).Count, "a reset (area change seen) starts over too");
            Equal(0, downs.Update(town, false, null).Count, "alone: the death screen's own reader");
        }

        private static void InputSeatFollowsWhoPressedLast()
        {
            (uint, float)? idle = (0u, 0f), a = (0x400u, 0f), walking = (0u, 1f);
            var alone = new InputSeat();
            Equal(1, alone.Update(true, false, a, a), "player 2 not read: player 1's controller");
            var second = new InputSeat();
            Equal(1, second.Update(false, true, a, idle), "player 2 read alone, before it presses: player 1 (it has not joined)");
            Equal(2, second.Update(false, true, idle, a), "player 2's first press");
            Equal(2, second.Update(false, true, a, idle), "then player 1's presses do not take it back");
            var both = new InputSeat();
            Equal(1, both.Update(true, true, idle, idle), "both read: player 1 first");
            Equal(1, both.Update(true, true, walking, a), "player 1 starts walking as player 2 presses A: both pressed, no change");
            Equal(1, both.Update(true, true, walking, idle), "player 1 keeps walking: holding a stick is not a press");
            Equal(2, both.Update(true, true, walking, (0x1u, 0f)), "player 2 moves in its menu");
            Equal(1, both.Update(true, true, (0x400u, 1f), idle), "player 1 presses A");
            Equal(1, both.Update(true, true, (0x400u, 1f), null), "player 2 unreadable: no press");
            Equal(2, both.Update(true, true, (0x400u, 1f), walking), "player 2 pushes a stick");
            both.Baseline();
            Equal(2, both.Update(true, true, (0x7u, 1f), idle), "another UI manager: its first reading is not a press");
            Equal(1, both.Update(true, true, (0x8u, 1f), idle), "then a press counts again");
        }

        private static void LocalPlayerListTrustsOnlyItsOwnPlayerFirst()
        {
            IntPtr self = 0x1000, partner = 0x2000, stranger = 0x3000;
            Equal((IntPtr?)partner, LocalPlayerList.Second(new[] { self, partner }, self), "couch co-op: the second entry");
            Equal((IntPtr?)null, LocalPlayerList.Second(new[] { self }, self), "alone: nobody");
            Equal((IntPtr?)null, LocalPlayerList.Second(new[] { stranger, partner }, self), "a list not led by the player is not trusted");
            Equal((IntPtr?)null, LocalPlayerList.Second(new[] { self, partner, stranger }, self), "more seats than the game has: not trusted");
            Equal((IntPtr?)null, LocalPlayerList.Second(new[] { self, IntPtr.Zero }, self), "an empty seat");
            Equal((IntPtr?)null, LocalPlayerList.Second(new[] { self, partner }, IntPtr.Zero), "no player yet");
        }

        private static void NearbyThingsAreNamedOncePerApproach()
        {
            var tracker = new ProximityTracker(200, 300);
            var checkpoint = new ScanEntry(1, ScanCategory.Waypoints, "Checkpoint", 1000, 0, 0);
            var stash = new ScanEntry(2, ScanCategory.Objects, "Stash", 1150, 0, 0);
            string Walk(float x) => string.Join(", ", tracker.Update(x, 0, new[] { checkpoint, stash }).Select(entry => entry.Name));
            Equal(string.Empty, Walk(0), "far away");
            Equal("Checkpoint", Walk(820), "walked up to the checkpoint");
            Equal("Stash", Walk(950), "then the stash comes within reach");
            Equal(string.Empty, Walk(1000), "standing on the checkpoint says nothing new");
            Equal(string.Empty, Walk(760), "stepping back inside the re-arm distance stays quiet");
            Equal(string.Empty, Walk(500), "walked away");
            Equal("Checkpoint, Stash", Walk(1050), "back again: both, nearest first");
        }
    }
}

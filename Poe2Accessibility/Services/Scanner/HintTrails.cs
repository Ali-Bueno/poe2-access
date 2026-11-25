namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     The map's cues that run somewhere (a river, a road, cracks, a swarm), followed from where the player meets them: said
    ///     with the part of the trail and the walk to where it leads, sounded toward the way while followed, listed in the
    ///     scanner (docs/research/map-hints.md, "Trails").
    /// </summary>
    public sealed class HintTrails
    {
        public const string SoundKey = "trail";

        private const int PollMs = 500;

        /// <summary>Two sounds a step and a half apart, told from the guide's denser ping.</summary>
        private const int SoundMs = 1600;

        /// <summary>The sound comes from where the walk has got this far on: the next bend, not the goal through a wall.</summary>
        private const int AheadCells = 40;

        /// <summary>Moving less than this between polls is standing still: half a second of running is 18 cells.</summary>
        private const float MovedCells = 3f;

        /// <summary>Closer than this the sound sits in the middle (LeftBehind's).</summary>
        private const float CentreWorld = 300f;

        /// <summary>A goal's name and place are looked up again this often: the game's own exit may have come into view.</summary>
        private const int GoalMs = 5000;

        /// <summary>This near where a swarm flew, it has led there: half of what the player sees.</summary>
        private const float ArrivedCells = ExplorationMap.LookCells / 2;

        /// <summary>The walks are worked out again no sooner, when the player stands on ground they do not reach (a door opened).</summary>
        private const int RebuildMs = 10000;

        /// <summary>Scanner ids: above the remembered exits, below the unexplored ground.</summary>
        private const uint IdBase = 0xB8000000;

        private readonly EntityCatalog catalog;
        private readonly IAccessibilityOutputService output;
        private readonly AudioCueService audio;
        private readonly PluginLog log;
        private readonly float[] sound;
        private readonly List<Trail> trails = new();
        private string instance = string.Empty;
        private Vector2 lastPlace;
        private bool moving;
        private long lastPoll;
        private long lastSound;

        public HintTrails(EntityCatalog catalog, IAccessibilityOutputService output, AudioCueService audio, PluginLog log)
        {
            this.catalog = catalog;
            this.output = output;
            this.audio = audio;
            this.log = log;
            this.sound = CueSynth.Trail(AudioCueService.SampleRate);
        }

        public float Gain { get; set; } = 1f;

        /// <summary>The trail sounds while it is followed.</summary>
        public bool Sounds { get; set; } = true;

        /// <summary>Forgets the trails: another area.</summary>
        public void Reset(string areaInstance)
        {
            this.trails.Clear();
            this.instance = areaInstance;
        }

        /// <summary>A trail of the area, not met yet.</summary>
        /// <param name="spots">Its places in grid cells, none for one that leads from where it appears.</param>
        public void Add(MapHintRow row, IReadOnlyList<Vector2> spots, Func<ScanEntry?> goal) => this.trails.Add(new Trail(row, spots, goal, (uint)this.trails.Count));

        /// <summary>The trails met and not yet followed to their end, for the scanner.</summary>
        public IEnumerable<ScanEntry> Entries() =>
            this.Open().Select(trail => trail.Goal! with
            {
                Id = IdBase + trail.Index,
                Category = ScanCategory.Quest,
                Name = Announcements.Trail(trail.Row.Hint, trail.Goal!.Name),
                Kind = SoundKey,
                Subject = string.Empty,
                QuestMarked = false,
            });

        /// <summary>How each trail met goes on from here, for the glance at the map.</summary>
        public IEnumerable<string> Lines() => this.Open().Select(this.Line);

        /// <summary>The trails and how far they run, for the dev command "maphints".</summary>
        public IEnumerable<string> Diagnose() => this.trails.Select(trail =>
            FormattableString.Invariant($"Trail {trail.Row.Hint}: {trail.Spots.Count} spots, goal {trail.Goal?.Name ?? "none"}, ends {trail.Ends}, met {trail.Follower.Known}, part {trail.Follower.Part}"));

        /// <summary>Per frame while playing out of towns.</summary>
        /// <param name="inFight">Enemies about: a step back from them is no wrong way (the lines of the way itself are said; packs are always near).</param>
        /// <param name="quiet">The guide leads, the one sound to follow, or the fight is one to hear.</param>
        public void Tick(bool playing, bool inFight, bool quiet)
        {
            var now = Environment.TickCount64;
            if (!playing || this.trails.Count == 0 || now - this.lastPoll < PollMs || !this.IsHere() || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            this.lastPoll = now;
            var here = new Vector2(player.GridPosition.X, player.GridPosition.Y);
            this.moving = Vector2.Distance(here, this.lastPlace) >= MovedCells;
            this.lastPlace = here;
            foreach (var trail in this.trails)
            {
                this.Follow(trail, here, now, quiet || inFight);
            }

            if (this.Sounds && !quiet && now - this.lastSound >= SoundMs &&
                this.Open().FirstOrDefault(trail => trail.Follower.Leads(now) && (this.moving || now - trail.FoundAt < TrailFollower.LeadInMs)) is { } lead &&
                lead.Field?.Ahead(here, AheadCells) is { } ahead)
            {
                this.lastSound = now;
                var offset = (ahead - here) * GameReader.Core.States.InGameStateObject.CurrentAreaInstance.WorldToGridConvertor;
                var (pan, pitch) = ScreenProjection.Voice(player.WorldPosition, offset, offset.Length(), CentreWorld);
                this.audio.Play(this.sound, pan, pitch, this.Gain, SoundKey);
            }
        }

        /// <summary>The trails are of the area the player is in: the cues are read again up to a second after a change of area.</summary>
        private bool IsHere() => GameReader.Core.States.InGameStateObject.CurrentAreaInstance.AreaHash == this.instance;

        private IEnumerable<Trail> Open() =>
            this.IsHere() ? this.trails.Where(trail => trail.Follower.Known && !trail.Over && trail.Goal != null) : Enumerable.Empty<Trail>();

        private void Follow(Trail trail, Vector2 here, long now, bool quiet)
        {
            var nearest = trail.Spots.Count == 0 ? 0f : trail.Spots.Min(spot => Vector2.Distance(spot, here));
            var sees = nearest <= ExplorationMap.LookCells;
            if (trail.Over || (!trail.Follower.Known && !sees) || !this.FindGoal(trail, now))
            {
                return;
            }

            // Its end is in view, or was known before the trail was met. A swarm's may have been passed before it was sent there.
            if (trail.Spots.Count > 0 ? this.catalog.IsSeen(trail.GoalCell) : Vector2.Distance(here, trail.GoalCell) <= ArrivedCells)
            {
                trail.Over = true;
                return;
            }

            if (!this.Walks(trail, here, now))
            {
                return;
            }

            switch (trail.Follower.Update(now, trail.Field!.Steps(here), sees, nearest <= trail.Row.Reach, trail.Ends))
            {
                case TrailNews.Found:
                    trail.FoundAt = now;
                    this.log.Info(FormattableString.Invariant($"Trail {trail.Row.Hint} met at cell {here.X:0},{here.Y:0}, walk {trail.Field.Steps(here)} of {trail.Ends}"));
                    this.output.Say(SpeechKind.Places, this.Line(trail), urgent: true);
                    break;
                case TrailNews.Part:
                    this.output.Say(SpeechKind.Places, this.Line(trail), urgent: true);
                    break;
                case TrailNews.Away when !quiet:
                    this.output.Say(SpeechKind.Places, Announcements.TrailWrongWay(this.Line(trail)), urgent: true);
                    break;
                case TrailNews.Left when !quiet:
                    var back = trail.Spots.MinBy(spot => Vector2.Distance(spot, here));
                    this.output.Say(SpeechKind.Places, Announcements.TrailLeft(trail.Row.Hint, this.catalog.SideToward(back)), urgent: true);
                    break;
            }
        }

        /// <summary>"River, half way, toward The Red Vale, 60 steps, right 5, then up right".</summary>
        private string Line(Trail trail)
        {
            var name = Announcements.TrailAt(trail.Row.Hint, trail.Spots.Count > 0 ? trail.Follower.Part : null, trail.Goal!.Name);
            return this.catalog.Describe(trail.Goal with { Id = IdBase + trail.Index, Name = name });
        }

        private bool FindGoal(Trail trail, long now)
        {
            if (trail.Goal == null || now - trail.GoalAt >= GoalMs)
            {
                trail.GoalAt = now;
                trail.Goal = trail.FindGoal() ?? trail.Goal;
                var toWorld = GameReader.Core.States.InGameStateObject.CurrentAreaInstance.WorldToGridConvertor;
                trail.GoalCell = trail.Goal != null && toWorld > 0 ? new Vector2(trail.Goal.WorldX, trail.Goal.WorldY) / toWorld : default;
            }

            return trail.Goal != null;
        }

        /// <summary>The walks to the trail's goal, worked out off the frame thread; false until they are there.</summary>
        private bool Walks(Trail trail, Vector2 here, long now)
        {
            if (trail.Work is { IsCompleted: true } done)
            {
                trail.Work = null;
                if (done.IsFaulted)
                {
                    this.log.Warn($"Trail {trail.Row.Hint}: {done.Exception?.GetBaseException().Message}");
                }
                else if (done.Result is { } built)
                {
                    (trail.Field, trail.Ends) = (built.Field, built.Ends);
                }
            }

            if (trail.Work == null && now - trail.BuiltAt >= RebuildMs && (trail.Field == null || trail.Field.Steps(here) == null) &&
                this.catalog.TryLiveGrid(out var grid, out var row))
            {
                trail.BuiltAt = now;
                var (goal, spots) = (trail.GoalCell, trail.Spots);
                trail.Work = Task.Run<(TrailField Field, (int Near, int Far) Ends)?>(() =>
                    TrailField.Build(grid, row, goal) is { } field ? (field, field.Ends(spots.Count > 0 ? spots : new[] { here }) ?? (0, 0)) : null);
            }

            return trail.Field != null;
        }

        private sealed class Trail
        {
            public Trail(MapHintRow row, IReadOnlyList<Vector2> spots, Func<ScanEntry?> goal, uint index) =>
                (this.Row, this.Spots, this.FindGoal, this.Index) = (row, spots, goal, index);

            public MapHintRow Row { get; }

            public IReadOnlyList<Vector2> Spots { get; }

            public Func<ScanEntry?> FindGoal { get; }

            public uint Index { get; }

            public TrailFollower Follower { get; } = new();

            public ScanEntry? Goal { get; set; }

            public Vector2 GoalCell { get; set; }

            public long GoalAt { get; set; } = long.MinValue / 2;

            public long FoundAt { get; set; }

            public bool Over { get; set; }

            public TrailField? Field { get; set; }

            public (int Near, int Far) Ends { get; set; }

            public Task<(TrailField Field, (int Near, int Far) Ends)?>? Work { get; set; }

            public long BuiltAt { get; set; } = long.MinValue / 2;
        }
    }
}

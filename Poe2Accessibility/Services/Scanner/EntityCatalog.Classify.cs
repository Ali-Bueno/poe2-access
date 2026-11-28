namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteEnums;
    using GameReader.RemoteEnums.Entity;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>How <see cref="EntityCatalog"/> sorts an entity into a scanner category and names it, and the component flags it reads.</summary>
    public sealed partial class EntityCatalog
    {
        /// <summary>How far from a cage the one it holds may be: Risu was one cell (about 11) from hers; the Karui elders are not measured.</summary>
        private const float CageWorld = 100f;

        /// <summary>How far (world units) the monster an essence holds stands from its crystal: the same grid cell.</summary>
        private const float EssenceWorld = EssencePrison.ReachWorld;

        /// <summary>Files of minimap marks the scanner left out, logged once each.</summary>
        private readonly HashSet<string> silentMarks = new(StringComparer.Ordinal);
        private readonly HashSet<string> namedFromPath = new(StringComparer.Ordinal);
        private readonly HashSet<string> spiritlessCorpses = new(StringComparer.Ordinal);
        private readonly HashSet<string> silentCharacters = new(StringComparer.Ordinal);
        private readonly HashSet<string> hostileCharacters = new(StringComparer.Ordinal);
        private readonly HashSet<string> shrineRows = new(StringComparer.Ordinal);
        private readonly HashSet<uint> essenceRows = new();
        private readonly Dictionary<uint, (bool HasProperties, Rarity Rarity, int ModCount, int CorruptByMods, bool HasMods, Rarity ModsRarity, int Implicit, int Explicit, bool HasStats, int CorruptByItems, int CorruptByBuffs)> strongboxReads = new();
        private readonly Dictionary<string, string> abouts = new(StringComparer.Ordinal);

        /// <summary>Ground items of this area the game's item filter hides, logged once each (<see cref="MaxLoggedFiltered"/> at most).</summary>
        private readonly HashSet<uint> filteredLogged = new();

        private const int MaxLoggedFiltered = 64;

        /// <summary>Whether the game's item filter hides items for this listing (<see cref="FollowItemFilter"/> and the game's option).</summary>
        private bool filterHides;

        /// <summary>
        ///     Category and name, or null for what the scanner leaves out (self, dead, opened, unnamed, and what is
        ///     not in play: the town holds every story variant of an NPC, only one of them targetable).
        /// </summary>
        private ScanEntry? Classify(Entity entity, Render render)
        {
            var path = entity.Path ?? string.Empty;
            var league = LeaguePieces.RuleOf(path) is { Lists: true } listed ? listed : null;
            // An Abyss fissure carries its icon closed or open: its transition lists it, not the icon.
            var leagueIcon = league?.Piece == LeaguePiece.AbyssCrack ? null : LeagueDeviceIcon(entity, path);
            if (leagueIcon == MinimapMarks.EssenceIcon && EssenceWatch.IsCrystal(entity) && EssencePrison.IsShattered(TransitionState.Of(this.memory, entity)))
            {
                return null; // shattered: the game hides its icon and deletes it five seconds later
            }

            // A league's altar stays listed out of play; what a quest icon marks does not: a Trial's reward is spent once used.
            // A dead monster's corpse is out of play too (nothing targets it), and is listed all the same: it is what
            // Bind Spectre and the corpse skills use, and the fight left it there to be found.
            var trap = TrialTraps.KindOf(path);
            var chaos = ChaosTrial.PieceOf(path);
            var foughtBeside = chaos is { } fought && ChaosTrial.IsFoughtBeside(fought);
            var corpse = IsCorpse(entity);
            if ((leagueIcon == null || MinimapMarks.IsQuestIcon(leagueIcon)) && trap == null && !foughtBeside && league is not { WithoutTarget: true } &&
                !corpse && !this.InPlay(entity) && !(IsCharacter(entity) && this.walking.Walks(entity.Id, new Vector2(render.WorldPosition.X, render.WorldPosition.Y))))
            {
                this.LogLeftOutCharacter(entity, path, "not in play");
                return null;
            }

            if (IsScenery(entity) && !this.walking.Walks(entity.Id, new Vector2(render.WorldPosition.X, render.WorldPosition.Y)))
            {
                this.LogLeftOutCharacter(entity, path, "scenery: no target and nothing to use");
                return null;
            }

            var name = this.elements.ReadWStringStructAt(render.Address + GameLayout.RenderComponent.NameOffset) ?? string.Empty;
            var kind = string.Empty;
            var plain = false;
            var subject = string.Empty;
            var baseId = string.Empty;
            var loot = LootTier.Valuable;
            var sound = string.Empty;
            var timed = false;
            var strongbox = false;
            var icon = entity.TryGetComponent<MinimapIcon>(out var minimap) ? minimap.IconName ?? string.Empty : string.Empty;
            var iconHidden = minimap != null && this.memory.TryRead<byte>(minimap.Address + GameLayout.MinimapIconRow.ComponentHiddenOffset, out var hidden) && hidden != 0;
            var questMarked = MinimapMarks.IsQuestIcon(icon);
            ScanCategory? category;
            if (icon == MinimapMarks.StoryGlyph)
            {
                // Typed NPC but a sign: "Pueblo de Ogham" by the road was listed as a character and taken for the quest's
                // village (user 2026-10-28).
                category = name.Length > 0 ? ScanCategory.Objects : null;
                name = Announcements.Sign(name);
                kind = MinimapMarks.SignKind;
            }
            else if (entity.EntityType == EntityTypes.Player)
            {
                category = Seats.IsFollowed(entity) ? null : ScanCategory.Players;
                if (entity.TryGetComponent<Player>(out var other) && other.Name.Length > 0)
                {
                    name = Announcements.OtherPlayer(other.Name, name, other.Level); // the render name is the class
                }
            }
            else if (ExitEntities.IsExit(entity, path))
            {
                category = ScanCategory.Exits;
                kind = "exit";
            }
            else if ((path.Contains("Waypoint", StringComparison.Ordinal) || path.Contains("Checkpoint", StringComparison.Ordinal) ||
                      path.Contains("Portal", StringComparison.Ordinal)) && !IsNpc(entity))
            {
                category = ScanCategory.Waypoints;
                kind = path.Contains("Checkpoint", StringComparison.Ordinal) ? "checkpoint" : path.Contains("Waypoint", StringComparison.Ordinal) ? "waypoint" : "portal";
            }
            else if (entity.TryGetComponent<WorldItem>(out var worldItem))
            {
                if (this.filterHides && this.HiddenByItemFilter(entity.Id, worldItem))
                {
                    // The game draws no label for it and offers no pick-up, yet the mod named it on approach (user 2026-11-28).
                    return null;
                }

                category = ScanCategory.Items;
                name = this.inventory.DescribeItemEntity(worldItem.ItemEntityAddress, name) ?? name;
                (kind, loot) = this.inventory.ItemKind(worldItem.ItemEntityAddress);
                GroundTooltip.Remember(this.itemTitles, GroundTooltip.TitleOf(name), kind == "gold" ? string.Empty : name); // "Oro, 2": the title before the amount

                if (this.inventory.ItemBaseName(worldItem.ItemEntityAddress) is { Length: > 0 } baseName)
                {
                    GroundTooltip.Remember(this.itemTitles, baseName, kind == "gold" ? string.Empty : name); // a rare's tooltip names its base on a line of its own
                }
                baseId = this.inventory.ItemBaseId(worldItem.ItemEntityAddress);
            }
            else if (entity.EntityType == EntityTypes.Chest)
            {
                var chest = entity.TryGetComponent<Chest>(out var component) ? component : null;
                category = chest is { IsOpened: true } ? null : ScanCategory.Chests;
                // A rock or an egg breaks like an urn and shows no label; it sounded like a chest (user 2026-10-26). A
                // switch or lever built as a chest (the mausoleum's secret door switch) sounded like a breakable (2026-10-27);
                // a body searched for loot sounded like a chest (2026-10-28).
                kind = Switches.IsSwitch(path) ? "switch" : Breakables.IsCorpse(path) ? "corpse"
                    : chest is { IsLabelVisible: false, IsStrongbox: false } ? "breakable" : "chest";
                sound = kind == "breakable" ? Breakables.Material(path) : kind == "corpse" ? Breakables.BodySound(path) : chest is { IsStrongbox: true } ? LeaguePieces.StrongboxSound
                    : kind == "chest" && LeaguePieces.IsAbyssChest(path) ? LeaguePieces.AbyssChestSound : string.Empty;
                strongbox = chest is { IsStrongbox: true };
                if (strongbox && LeaguePieces.IsStrongboxSpent(StatesOf(entity)))
                {
                    category = null; // looted by its script though the Chest component still reads unopened (a landmark box)
                }

                if (strongbox && category != null)
                {
                    var properties = entity.TryGetComponent<ObjectMagicProperties>(out var magicProperties) ? magicProperties : null;
                    this.LogStrongbox(entity, properties);
                    // The sighted label is coloured by rarity; the component the host reads carries it (confirmed
                    // live: Normal on a box offering only transmutation/alchemy buttons, 2026-11-23). Normal says
                    // nothing, as a normal item says nothing; an unread name stays unread, so the box is dropped
                    // below rather than listed as a bare rarity.
                    if (name.Length > 0 && properties != null &&
                        properties.Rarity is Rarity.Magic or Rarity.Rare or Rarity.Unique)
                    {
                        subject = name; // an objective names the box without its rarity
                        name = Announcements.WithRarity(name, properties.Rarity);
                    }
                }
            }
            else if (entity.EntityType == EntityTypes.Shrine)
            {
                category = entity.TryGetComponent<Shrine>(out var shrine) && shrine.IsUsed ? null : ScanCategory.Chests;
                kind = "shrine";
                if (shrine != null && this.ShrineNameOf(shrine.Address) is { Length: > 0 } shrineName)
                {
                    name = shrineName; // which shrine it is, before deciding to take it
                }
            }
            else if (leagueIcon != null)
            {
                // Listed while the game makes it untargetable too (during the ritual): it is where the fight is and,
                // once done, where its tribute is spent ("Interactúa para ver los favores").
                category = ScanCategory.Encounters;
                if (leagueIcon == MinimapMarks.EssenceIcon)
                {
                    // "Esencia: Cretino de sangre": the monster held is what the game names on the crystal.
                    var crystal = StatesOf(entity).ToList();
                    name = Announcements.Essence(
                        this.NameNear(render, EssenceWorld, IsHeldMonster),
                        crystal.FirstOrDefault(state => state.Name == LeaguePieces.EssenceCountState).Value,
                        crystal.FirstOrDefault(state => state.Name == LeaguePieces.EssenceCorruptedState).Value > 0,
                        this.EssenceNamesOf(entity));
                }

                name = name.Length > 0 ? name : this.landmarks.WorldTitle(path);
                name = name.Length > 0 ? name : Announcements.LeagueDevice(leagueIcon, path.Contains(BossDevice, StringComparison.Ordinal),
                    ObjectNames.LeagueName(minimap is null ? string.Empty : MinimapIconLabel.Of(this.memory, this.elements, minimap), path));
                kind = MinimapMarks.DeviceKind(leagueIcon);
                sound = leagueIcon == MinimapMarks.EssenceIcon ? LeaguePieces.EssenceSound : LeaguePieces.SoundOf(path);
            }
            else if (league != null)
            {
                // A league's object the rules above miss, while its states say it is there to find (docs/research/league-mechanics.md
                // §12); an Expedition marker by what its icon says it buries, whatever its own name.
                var leagueStates = TransitionState.With(this.memory, entity);
                // The one creature here is an Azmeri spirit, a wisp nothing can hit: not listed, it is nothing, never an enemy.
                var model = league.Piece == LeaguePiece.ExpeditionMarker ? ModelOf(entity) : string.Empty;
                var markerIcon = league.Piece == LeaguePiece.ExpeditionMarker ? LeaguePieces.MarkerIcon(icon, model) : icon;
                var spent = ExpeditionReach.Detonated && league.Piece is LeaguePiece.ExpeditionMarker or LeaguePiece.ExpeditionBlastObject;
                category = !spent && LeaguePieces.IsListed(league, leagueStates, markerIcon) ? ScanCategory.Encounters : null;
                var find = LeaguePieces.FindOf(league.Piece, markerIcon);
                name = league.Piece == LeaguePiece.ExpeditionMarker ? string.Empty : name.Length > 0 ? name : this.landmarks.WorldTitle(path);
                name = name.Length > 0 ? name
                    : find == ExpeditionFind.Chest && LeaguePieces.ChestOf(model) is { } chest ? Announcements.ExpeditionChestName(chest)
                    : Announcements.LeaguePieceName(league.Piece, find);
                if (find != null && (ExpeditionReach.IsCovered(entity.Id) || leagueStates.Any(state => state.Name == LeaguePieces.CoveredState && state.Value > 0)))
                {
                    name = Announcements.ExpeditionCovered(name); // the charges placed already reach it: the next goes elsewhere
                }

                kind = league.Piece is LeaguePiece.ExpeditionMarker or LeaguePiece.ExpeditionBlastObject ? LeaguePieces.MarkerKind : MinimapMarks.DeviceKind(icon);
                sound = LeaguePieces.SoundOf(path);
            }
            else if (foughtBeside)
            {
                // Flagged hidden, with no target and no minimap mark, yet the round is fought beside it: the statue to
                // escort, a blood circle until it is full (docs/research/trial-of-chaos.md).
                category = ChaosTrial.StillAsks(chaos!.Value, StatesOf(entity)) ? ScanCategory.Objects : null;
                name = name.Length > 0 ? name : Announcements.ChaosPieceName(chaos.Value);
                var lift = chaos.Value == ChaosPiece.Lift;
                sound = lift ? string.Empty : MinimapMarks.QuestObjectKind;
                kind = lift ? ChaosTrial.LiftSound : kind;
                questMarked = !lift; // a lift sounds as itself, without the quest object's layer
            }
            else if (PowderBarrels.Is(path))
            {
                kind = PowderBarrels.KindOf(StatesOf(entity)) ?? string.Empty;
                category = kind.Length > 0 ? ScanCategory.Objects : null;
                name = kind == PowderBarrels.LitKind ? Announcements.LitPowderBarrel : Announcements.PowderBarrel;
            }
            else if (trap is { } kindOfTrap)
            {
                // Untargetable and flagged hidden, yet there on the floor for anyone who can see.
                category = TrialTraps.IsSeenBeforeItStrikes(kindOfTrap) ? ScanCategory.PointsOfInterest : null;
                name = Announcements.TrialTrapName(kindOfTrap);
                kind = TrapKind;
            }
            else if (entity.EntityType == EntityTypes.Monster && (path.StartsWith(CreatureFolder, StringComparison.Ordinal) || !entity.TryGetComponent<MinimapIcon>(out _)))
            {
                // A "monster" outside the creatures that the minimap marks is a device: it goes on as a plain object below.
                // A friendly monster with the NPC component is a character (Mortimer by the Miller); minions have none. So is one
                // to talk to that the player cannot attack, whatever side it reads: Sekhema Asala in the Dreadnought has no NPC
                // component, only InteractionAction (MarakethMatriarchDreadnought_.ot, issue #7).
                var npc = entity.TryGetComponent<NPC>(out _);
                category = corpse ? ScanCategory.Corpses
                    : IsAliveEnemy(entity) ? ScanCategory.Enemies : npc || IsTalkable(entity) ? ScanCategory.Npcs : null;
                if (category == ScanCategory.Enemies && IsCharacter(entity))
                {
                    this.LogCharacterAsEnemy(entity, path);
                }

                if (category == ScanCategory.Corpses)
                {
                    kind = CorpseKind;
                    questMarked = false; // a corpse is never what a quest asks for, whatever icon it died under
                    var (spirit, variety) = SpectreCatalog.Shared.Lookup(path);
                    if (spirit == null && name.Length > 0 && this.spiritlessCorpses.Add(BossMechanicsCatalog.MonsterId(path)))
                    {
                        this.log.Info($"Corpse without a spirit cost: {path}");
                    }

                    name = name.Length > 0 ? Announcements.CorpseOf(name, spirit, variety?.Weapon ?? string.Empty, variety?.Attack ?? string.Empty) : name;
                }
                if (category == ScanCategory.Enemies && entity.TryGetComponent<ObjectMagicProperties>(out var magic) && magic.Rarity != Rarity.Normal)
                {
                    name = Announcements.EnemyWithRarity(name, magic.Rarity);
                }
            }
            else if (IsNpc(entity))
            {
                category = ScanCategory.Npcs;
            }
            else if (!MinimapMarks.IsQuestIcon(icon) && entity.TryGetComponent<TriggerableBlockage>(out var blockage))
            {
                // Tutorial beams and gates: where the way is shut, while it is (BarrierWatch says when one opens).
                // A barrier stays one for the guide, so what the story wants opened sounds as a quest object through its Sound: the
                // Ancient Seal. Under the quest's icon it is no barrier but a quest object: Balbala's three Runic Seals have this
                // component too, walk-through and not blocked, and were listed nowhere, so the fight's start was found by luck (user 2026-11-07).
                category = blockage.IsBlocked ? ScanCategory.Objects : null;
                // A door the player opens is found by ear: shut, it sounds where it stands (the Lost City's, user 2026-11-14).
                sound = name.Length > 0 && MinimapMarks.IsStoryBarrier(path) ? MinimapMarks.QuestObjectKind
                    : entity.TryGetComponent<Targetable>(out _) ? ClosedDoors.Kind : CueSynth.BarrierSound; // the rest: heard in a loop while shut (owner, 2026-11-19)
                name = name.Length > 0 ? name : Announcements.ClosedBarrier;
                kind = "barrier";
            }
            else
            {
                // Stash, bench, well; and a named thing under a lit mark with nothing to click, stepped on instead: the Matlan
                // Waterways' "Mecanismo del canal" pressure plates (`VaalPressurePlateActive`), left out (the owner, 2026-11-19).
                var stepped = name.Length > 0 && MinimapMarks.IsLitMark(icon) && !iconHidden && league == null && chaos == null;
                category = (entity.TryGetComponent<Targetable>(out _) || stepped) && !IsPet(path) ? ScanCategory.Objects : null;
                plain = true;
                kind = MinimapMarks.ServiceKind(icon);
                // A named object to use under an area's own lit mark is the quest's: the Red Vale's obelisks rang only while
                // the tracker named them, and "Consigue las dos runas de poder restantes" does not (user 2026-11-13).
                questMarked |= category == ScanCategory.Objects && name.Length > 0 && kind.Length == 0 && MinimapMarks.IsLitMark(icon) && !iconHidden;
                // A named object made for this area alone, its file in the area's own Objects folder, is the story's: El cruce de
                // Khari's "Sello de señalización" had no quest icon, and its tracker step spoke of a "roca" (owner, 2026-11-26).
                questMarked |= category == ScanCategory.Objects && name.Length > 0 && kind.Length == 0 && IsAreaOwnObject(path, this.areaId());
                questMarked |= category == ScanCategory.Objects && KeyPedestals.Is(path) && this.QuestItemsCarried().Count > 0;
                questMarked |= category == ScanCategory.Objects && KeyPedestals.TakesCarried(name, this.QuestItemsCarried());
                if (this.landmarks.ObjectiveOf(path) is { Length: > 0 } objective)
                {
                    // A Trial's rooms are grounds apart on one floor: a crystal the player can walk to is this room's, one on
                    // other ground another room's, whatever the tracker shows (docs/research/trials-and-campaign.md, crystals).
                    // The tracker's objective and the HUD's count decide only while the player's ground is not known yet.
                    var own = this.OnPlayersGround(render);
                    if (own == false || (own == null &&
                        !this.QuestTexts().Any(text => text.Contains(objective, StringComparison.Ordinal)) && !this.CrystalsCounted()))
                    {
                        return null; // a Death Crystal in a room of another kind, called out from 352 steps off (user 2026-11-07)
                    }

                    timed = true;
                }

                // Levers in Trial rooms open the way out: SanctumGenericLever, a plain object, sounded nothing, and must stand out (user 2026-11-07).
                sound = Switches.IsSwitch(path) ? MinimapMarks.QuestObjectKind : sound;
                if (chaos is { } piece)
                {
                    // What the round asks for next rings, and a Stone Altar against the damage of the core in hand.
                    var (rings, clocked) = ChaosTrial.Asks(piece, this.HoldsSoulCore(), StatesOf(entity));
                    if (piece == ChaosPiece.LiftLever && this.LiftAwayFrom(render) is { } away)
                    {
                        // It only calls the lift: silent, and no target, while the lift stands by it.
                        rings &= away;
                        name = Announcements.ChaosCallsLift(name.Length > 0 ? name : Announcements.ChaosPieceName(piece));
                    }

                    sound = rings ? MinimapMarks.QuestObjectKind : string.Empty;
                    questMarked = rings;
                    timed = clocked;
                    name = name.Length > 0 ? name : Announcements.ChaosPieceName(piece);
                }
                if (name.Length == 0 && TrialWater.IsDrop(path))
                {
                    name = this.landmarks.WaterLabel; // the game's own word for what the drop is: "Agua sagrada"
                }

                if (name.Length == 0)
                {
                    // Named only in a WorldDescription, which the mod cannot read: the real Relic Altar, after its named stand-in leaves
                    // play on the first use, and the Trial's Death Crystals (user 2026-11-07). The game explains such an object in a box
                    // of its own because it is there to be used, so it sounds as a quest object: the Blood Chalice, the Sand Hourglass.
                    name = this.landmarks.WorldTitle(path);
                    sound = name.Length > 0 ? MinimapMarks.QuestObjectKind : sound;
                }
            }

            // Only a plain object can be a mark: a used shrine, an opened chest or a minion stays out whatever its icon says.
            // A mark whose object says it is done (the Azak Bog's central effigy, burned=1 under its quest icon) is no landmark.
            if (plain && (category == null || name.Length == 0) && icon.Length > 0 && !iconHidden && !MarkDone.Is(StatesOf(entity)) &&
                MinimapMarks.PointOfInterest(path, icon, this.MarkLabel(path, icon, minimap!, render)) is { } point)
            {
                // A boss's arena, a secret or a quest landmark as the minimap labels it: the objects carry no name of their own.
                category = ScanCategory.PointsOfInterest;
                kind = point.Kind;
                subject = point.Kind == MinimapMarks.BossRoomKind ? point.Label : string.Empty;
                // A quest mark says what its object is for when the object has a name ("Apuñalar corazón sacrificial").
                name = point.Kind == MinimapMarks.QuestObjectKind && name.Length > 0 ? name
                    : point.Kind != MinimapMarks.BossRoomKind ? point.Label
                    : point.Defeated ? Announcements.BossRoomDefeated(point.Label) : Announcements.BossRoom(point.Label);
            }

            // A nameless piece of a boss's arena (Geonor's ice walls) is the fight's to tell, "something to break", not a file's word.
            if (plain && category == ScanCategory.Objects && name.Length == 0 && BossMechanicsCatalog.Shared.PlaceRules(BossMechanicsCatalog.MonsterId(path)).Count == 0)
            {
                // A new interactable no table names yet: its metadata file name, so it is heard instead of dropped.
                name = ObjectNames.FromPath(path);
                if (name.Length > 0 && this.namedFromPath.Add(path))
                {
                    this.log.Info($"Object named from its file: {path} -> {name}");
                }

                name = ObjectNames.Spoken(name, Localization.Language, Announcements.UnnamedObject, Announcements.UnnamedObjectWord);
            }

            if (category == null || name.Length == 0)
            {
                if (icon.Length > 0 && !MinimapMarks.IsSpent(icon) && !path.StartsWith(CreatureFolder, StringComparison.Ordinal) && this.silentMarks.Add(path))
                {
                    // What the minimap draws and the scanner does not list may be a quest's target: logged to find the gap.
                    this.log.Info($"Minimap mark not listed: {path} (icon {icon}, {entity.EntityType})");
                }

                this.LogLeftOutCharacter(entity, path, category == null ? $"no category, {entity.EntityType}" : "no name");
                return null; // unnamed placeholders are not things a player can find
            }


            if (category == ScanCategory.Npcs && this.HasQuestMarker(entity))
            {
                // D2R's NPC "!": the game's quest indicator over the head (someone to talk to for the story).
                name = Announcements.NpcWithQuest(name);
                kind = "npc_quest";
            }
            else if (category == ScanCategory.Npcs && this.IsEscort(entity.Id))
            {
                // An escort walks ahead with no marker over her head: she rings as a character with a quest, so she is
                // followed by ear (the owner after Alva in the past, 2026-11-20: "el sonidito de npc para cuando tenga que seguirla").
                kind = "npc_quest";
            }

            var position = render.WorldPosition;
            name = ControllerGlyphs.InName(name);
            return new ScanEntry(entity.Id, category.Value, name, position.X, position.Y, position.Z, MinimapMarks.KindUnder(icon, kind), baseId, loot, sound,
                questMarked, subject.Length > 0 || category != ScanCategory.Encounters || LeaguePieces.RuleOf(path)?.Piece != LeaguePiece.VaalBeacon
                    ? subject
                    // "Activa cinco focos antiguos adicionales": Act 3's quest names the beacons by their other title, not
                    // "Foco vaal", and they sounded as an encounter it does not ask for (owner, 2026-11-18).
                    : this.landmarks.WorldTitle(LeaguePieces.AncientBeaconTitle),
                timed, category is ScanCategory.Encounters or ScanCategory.Objects ? this.AboutOf(path) : string.Empty,
                category is ScanCategory.Encounters or ScanCategory.Objects ? CostOf(entity) : 0, Strongbox: strongbox,
                Dimmed: category == ScanCategory.Objects && MinimapMarks.IsSpent(icon));
        }

        /// <summary>What the object asks in sacred water, from its script's shared state; 0 without one.</summary>
        private static int CostOf(Entity entity) => ShrineCost.Of(StatesOf(entity));

        /// <summary>Whether the Trial of Chaos lift by this lever is on another floor (<see cref="ChaosTrial.LiftAway"/>); null with none near.</summary>
        private bool? LiftAwayFrom(Render lever)
        {
            var lifts = new List<Vector3>();
            foreach (var (_, other) in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities)
            {
                if (other.IsValid && ChaosTrial.PieceOf(other.Path ?? string.Empty) == ChaosPiece.Lift && other.TryGetComponent<Render>(out var lift))
                {
                    lifts.Add(new Vector3(lift.WorldPosition.X, lift.WorldPosition.Y, lift.WorldPosition.Z));
                }
            }

            return ChaosTrial.LiftAway(new Vector3(lever.WorldPosition.X, lever.WorldPosition.Y, lever.WorldPosition.Z), lifts);
        }

        /// <summary>The states of the object's script as they stand; none without a StateMachine.</summary>
        private static IEnumerable<(string Name, long Value)> StatesOf(Entity entity) =>
            entity.TryGetComponent<StateMachine>(out var machine) ? machine.States.Select(state => (state.Name, state.Value)) : Array.Empty<(string, long)>();

        /// <summary>The player carries a Vaal Soul Core of the Trial of Chaos (<see cref="ChaosTrial.HoldingCoreBuff"/>).</summary>
        private bool HoldsSoulCore() =>
            Seats.Followed.TryGetComponent<Buffs>(out var buffs) && buffs.StatusEffects.ContainsKey(ChaosTrial.HoldingCoreBuff);

        /// <summary>What the game writes of an object under its title, by its file: asked of every object at each listing, so kept.</summary>
        private string AboutOf(string path)
        {
            if (!this.abouts.TryGetValue(path, out var about))
            {
                this.abouts[path] = about = Announcements.ObjectDescription(this.landmarks.WorldBody(path));
            }

            return about;
        }

        /// <summary>
        ///     What the minimap labels a mark. The quest's own icon needs no label to be what the quest wants: Risu's cage in the
        ///     Mawdun Mine has none, no name and an empty world label, and was found by neither the scanner nor the button; said
        ///     as a bare "Objeto de misión" it did not tell what it was (user 2026-11-07).
        /// </summary>
        private string MarkLabel(string path, string icon, MinimapIcon minimap, Render render)
        {
            var label = MinimapIconLabel.Of(this.memory, this.elements, minimap);
            if (label.Length > 0 || !MinimapMarks.IsQuestIcon(icon))
            {
                return label;
            }

            if (!MinimapMarks.IsCage(path))
            {
                return Announcements.QuestObject;
            }

            // The character at a cage: caged, they are out of play and listed nowhere.
            var held = MinimapMarks.BareName(this.NameNear(render, CageWorld, other => other.EntityType == EntityTypes.NPC));
            return held.Length > 0 ? Announcements.CageOf(held) : Announcements.Cage;
        }

        /// <summary>
        ///     Whether the game's item filter hides this ground item, read as the game reads it (<see cref="GameLayout.WorldItemComponent"/>):
        ///     the component's verdict in one read, then the item's keep byte only for an item the verdict hides. Anything unread hides nothing.
        /// </summary>
        private bool HiddenByItemFilter(uint id, WorldItem worldItem)
        {
            var start = GameLayout.WorldItemComponent.SpanStart;
            var span = this.memory.ReadBytes(worldItem.Address + start, GameLayout.WorldItemComponent.SpanLength);
            if (span.Length < GameLayout.WorldItemComponent.SpanLength ||
                !GameItemFilter.VerdictHides(span[GameLayout.WorldItemComponent.FilterReadyOffset - start],
                    span[GameLayout.WorldItemComponent.FilterResultOffset - start], span[GameLayout.WorldItemComponent.NeverHiddenOffset - start]))
            {
                return false;
            }

            if (worldItem.ItemEntityAddress == IntPtr.Zero ||
                !this.memory.TryRead<byte>(worldItem.ItemEntityAddress + GameLayout.WorldItemComponent.ItemNeverHiddenOffset, out var kept) || kept != 0)
            {
                return false;
            }

            if (this.filteredLogged.Count < MaxLoggedFiltered && this.filteredLogged.Add(id))
            {
                // How a fresh drop the filter hides is confirmed from a session's log.
                this.log.Info($"Ground item hidden by the game's item filter: {id} {this.inventory.ItemBaseName(worldItem.ItemEntityAddress)}");
            }

            return true;
        }

        /// <summary>
        ///     The shrine's name in the client's language, from the row the server gave it; empty when the row does not read as one
        ///     the catalog knows. Each Id read, known or not, is logged once: how the field is confirmed from a session's log.
        /// </summary>
        private string ShrineNameOf(IntPtr component)
        {
            var id = ShrineRow.IdOf(this.memory, this.elements, component);
            var known = ShrineCatalog.Shared.ById(id);
            if (this.shrineRows.Add(id))
            {
                this.log.Info($"Shrine row: {(id.Length == 0 ? "unread" : $"\"{id}\"")}, {(known == null ? "not in the catalog" : "known")}");
            }

            return known == null ? string.Empty : ShrineCatalog.Shared.NameOf(known, Localization.Language);
        }

        /// <summary>Feeds the entity name check (judged once on enough monsters, characters and players).</summary>
        private void CheckLayout(Entity entity, Render render, List<IntPtr> renders)
        {
            if ((entity.EntityType == EntityTypes.Monster && IsAliveEnemy(entity)) || entity.EntityType is EntityTypes.NPC or EntityTypes.Player)
            {
                renders.Add(render.Address);
            }
        }
    }
}

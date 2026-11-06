# Surviving PoE2 patches: web and local-source study (2026-11-06)

Legend: PROVEN = read in code/docs/page, linked. UNCERTAIN = inferred or not verifiable from here.

## 1. How other memory tools survive patches

### GameHelper2 (Gordin) - local clone `external/GameHelper2`
- PROVEN: two layers (CLAUDE.md "Memory offset / patch-day recovery", lines ~65-72).
  1. 6 signature patterns in `GameOffsets/StaticOffsetsPatterns.cs`, found in Ghidra by starting from a string XREF
     ("Unable to get InGameState", "Mods.dat", "Got Instance Details from login server") and writing a wildcard pattern for the
     function; a `^` token marks the RIP-relative bytes to skip. The file's git history shows it changed on only 5 days
     (2026-03-03, 05-30, 06-02, 06-03, 09-04), so patterns survive most patches.
  2. Struct field offsets (`AreaInstanceOffsets.cs`, `UiElementBaseOffset.cs`, ...) are edited by hand each patch. CLAUDE.md says the
     Ghidra script does NOT cover them; they are recovered from live memory with in-code scanners.
- PROVEN: the `OffsetRecoveryFramework.java` Ghidra script is NOT public. It lives at
  `C:\Users\Gordin\GameHelper2-Offset-Recovery-Framework` (CLAUDE.md:69). `gh search repos` and `gh search code` find no repo of that name;
  the only code hit is the CLAUDE.md line itself. The same file says the procedure is in the author's private Claude memory.
- PROVEN: since 2026-07-09 GameHelper2 ships an automatic checker, `GameHelper/Ui/OffsetHelper.cs` + `OffsetHelperEngine.cs`
  (2.5k lines). It reflects over the `[FieldOffset]` structs, reads live instances and applies value anchors: component EntityPtr must equal
  the owning entity, StdVector First<=Last<=End in range, MSVC map sentinel shape, player path starts with `Metadata/`, UI Self pointer
  equals its own address. Commit `ae3b825` (2026-07-17) "Let's OffsetHelper find some shifted offsets on it's own" adds a +-0x200 recovery scan
  (`RecoveryScanRadius`). This is the same idea as our `Found again at runtime` table, built by upstream, and it is interactive (a human reads the verdict).
- Upstream patch-day history (git log of the clone, dates are commit dates; game patch dates from the patch-notes aggregator below):

  | Game patch | Game date | Upstream commit | What changed |
  |---|---|---|---|
  | 0.5.1 | ? | 2026-06-01/05 "updated some offsets for 0.5.1" (several commits, one reverted) | misc UI/radar offsets |
  | 0.5.4 | ? | 2026-06-25 `4183681` | 3 lines in `AreaInstanceOffsets.cs` (+version bump) |
  | 0.5.4c | ? | 2026-07-17 `3293e56` | `AreaInstanceOffsets.cs`: Environments 0x4C0->0x4C8, PlayerInfo 0x598->0x5A0, Entities 0x6D8->0x6E0, Terrain 0x8B8->0x8C0 (+8 each, one inserted field) |
  | 0.5.5 | 2026-09-02, hotfixes to 09-04 | 2026-09-04 `a99b8ba` "updated (most?) Offsets for 0.5.5", then fixes 09-04, 09-05 (x4), "Bump v2.7.0" | 11 files, 155 lines: GameStates, AreaInstance, ImportantUiElements, UiElementBase (18 lines), MapUi, SkillTreeNode, `StaticOffsetsPatterns.cs` (Game States pattern: allocator immediate 0x140->0x148 wildcarded) |
  | 0.5.5b-e | 09-11 to 10-05 | 2026-09-11 "Offset update for Controller mode" (2.7.5) | 1 offset |
  | 0.5.5f | 2026-11-05/06 | none yet in the clone (last commit 2026-09-15, v2.8.0) | GitHub says pushed 2026-09-14 (`gh api repos/Gordin/GameHelper2`) |
- Pattern: upstream reacts within 0-2 days for major patches (0.5.5: same day as the hotfix wave, polished through 09-05); small hotfixes are skipped
  when nothing moved. The most common mover is `AreaInstance` (+8 in 0.5.4c, shifted again in 0.5.5 and 0.5.5f, per our docs/compatibility.md).
  Note: the clone has had no upstream commit for 7+ weeks, so 0.5.5b-f broke us with no upstream fix. UNCERTAIN whether Gordin is still active.

### ExileApi / PoeHUD (PoE1) - [Qvin0000/ExileApi](https://github.com/Qvin0000/ExileApi) (last push 2022-12)
- PROVEN: `Core/PoEMemory/Offsets.cs` holds hand-written byte patterns with `?` masks and the disassembly in comments
  (BasePtr, FileRoot, AreaChange, ...), each with an optional minimum build number. Struct offsets are in `GameOffsets/*.cs`. No automatic
  derivation. The repo went dark in 2022; the maintained successor was closed.
### ExileCore2 (PoE2) - [exCore2/ExileCore2](https://github.com/exCore2/ExileCore2)
- PROVEN: the repo is binaries only (`ExileCore2.dll`, `GameOffsets2.dll`, `Loader.exe`, 2 commits, last 2026-05-29). Plugins
  (exCore2/Radar pushed 2026-09-16, ReAgent 09-06, ExpeditionIcons 10-12) are open. Our own `docs/research/exilecore2-study.md` records
  obfuscated `[FieldOffset]`s and an opaque `sig.bin`. UNCERTAIN how it is updated; the project is Discord-gated. No evidence of automatic derivation.
### POE2Radar (local `external/POE2Radar`)
- PROVEN (`CLAUDE.md`, `docs/`): AOB scan of `.text` for RIP-relative refs to find the GameState slot (`Game/AobScanner.cs`, `AobPatterns.cs`),
  validated by "a working chain"; struct offsets in `Poe2Offsets.cs` marked when confirmed live; a `POE2Radar.Research` CLI re-derives them by
  value scan (`--hp N`), `--chain`, `--find-entities/--find-terrain/--find-map`. Manual, but the same value-scanning idea we use.
### ExileBridge, StashUtility
- ExileBridge README: "diagnostic endpoints for offset validation", `tools/sync-game-data` + ggpk-extractor for game data after a patch. No signature or offset
  derivation found. StashUtility: a plugin, no memory layer.
### Original GameHelper (zaafar)
- Could not locate the repo through `gh` (404); GameHelper2's gitlab history starts 2026-03-03 as "Initial commit". Nothing verified.
### Other
- New GitHub repos matching ExileCore2 are all plugins (bnc-anonymous, ParogDev, devklex). No public RTTI walker, offset dumper or
  runtime struct scanner for PoE2 was found on GitHub search (`gh search repos` queries: "poe2 memory reader", "path of exile 2 offsets": zero hits).

## 2. Data ecosystem (game data files, NOT process memory)
- PROVEN: [poe-tool-dev/dat-schema](https://github.com/poe-tool-dev/dat-schema) is the schema of the `.datc64` tables. CI publishes
  `schema.min.json`, `schema-poe1.min.json`, `schema-poe2.min.json`, `schema.jsonl` at
  `https://github.com/poe-tool-dev/dat-schema/releases/download/latest/` (assets updated 2026-11-05 15:37Z). The repo has a `dat-schema/poe2/` folder.
  Format: GraphQL-like, typed in `src/types.ts`. Fetchable at start with ETag (the Rust crate `poe_data_tools` does this, 1 h cache).
- Follows patches: commits "0.5.5 Additions" 2026-09-08 (6 days after the patch), "PoE2: name the Incursion Wordlists" 09-15, "Fix 28 tables the game
  client reads differently" 10-29, last commit 2026-11-05. Hand-maintained by volunteers, with column names added over weeks. It lists columns of data tables
  (Mods, BaseItemTypes ...), it says nothing about runtime C++ object layouts, so it cannot repair `AreaInstance` or UI element offsets.
  It could help only where the mod reads a dat table row from memory (rows are laid out per this schema). UNCERTAIN whether we read any.
- [SnosMe/poe-dat-viewer](https://github.com/SnosMe/poe-dat-viewer) (pushed 2026-10-19): reads .datc64 from PoE1 and PoE2, imports from the patch server,
  "performs file analysis to find possible columns". Has an export library (lib/README).
- [repoe-fork/repoe](https://github.com/repoe-fork/repoe) (pushed 2026-10-25): JSON of stats/mods/items/gems, built with PyPoE plus the dat-schema. brather1ng/RePoE is stale (2022). PyPoE
  is now at Project-Path-of-Exile-Wiki/PyPoE (the OmegaK path gives 404). poe2db, poe.ninja: not checked in code; they publish site data, no schema or
  per-patch diff API found. GGG's public API (api.pathofexile.com, OAuth) covers profiles, stashes, trade; PoE2 game data is not offered. UNCERTAIN: no
  machine-readable "what changed in a patch" feed was found anywhere; patch notes are forum prose.

## 3. Patch 0.5.5f, 0.5.5e, cadence, 1.0
- PROVEN, [official 0.5.5f thread](https://www.pathofexile.com/forum/view-thread/4012719) (Kieren_GGG, 2026-11-05): a single bullet,
  "Added support for the upcoming announcement and new Supporter Packs." Xbox build delayed. A client update, so the exe changed; nothing technical stated.
  NOTE: the aggregator [poepatchnotes](https://poepatchnotes.pages.dev/poe2.html) says "Minimap landscape rendering fix" for 0.5.5f; the official
  first post does not say that. Trust the forum.
- PROVEN, [0.5.5e](https://www.pathofexile.com/forum/view-thread/4009785) (2026-10-05): Texture Quality setting for low VRAM, Ritual/Tomb/slow area load/decoration fixes.
  [0.5.5e Hotfix 1](https://www.pathofexile.com/forum/view-thread/4012026) 2026-10-28; [0.5.5d](https://www.pathofexile.com/forum/view-thread/4008534) 2026-09-27/28.
- Cadence (aggregator list, secondary source): 0.5.5 on 09-02, then 10 hotfixes in 6 days, b 09-11, c 09-17, d 09-28, e 10-05, e-hotfix 10-28, f 11-05:
  roughly one client build every 1-3 weeks, plus server-only maintenance. Which of these changed the exe layout: only history, e.g. 0.5.5f broke AreaInstance (ours).
- 1.0: secondary sources ([playnews.gg](https://www.playnews.gg/en/release-dates/path-of-exile-2-launches-in-1-0-on-december-11-and-goes-fully-free-to-play),
  [pushsquare](https://www.pushsquare.com/news/2026/08/path-of-exile-2-reaches-1-0-on-11th-december-goes-free-to-play-on-ps5)) report 1.0 on **2026-12-11**, free to play, PC/PS5/Xbox (Gamescom ONL announcement).
  Not checked on a GGG page. UNCERTAIN: no statement found about engine, client rewrite or anti-cheat changes at 1.0. Expect a big structural patch around then; plan for it.
- Policy, [forum 3474232 on third-party tools](https://www.pathofexile.com/forum/view-thread/3474232) and others: GGG says it does not endorse tools, cannot guarantee
  any is allowed, bans at its discretion; ToS forbids modifying the client or data. No statement exists that clears memory readers. Our `docs/research/ecosystem-and-policy.md`
  (2026-09-14) already records no documented ban wave for readers and no kernel anti-cheat. Not re-verified beyond that.
- Accessibility: no GGG statement on screen readers or blind play found. Related: forum threads
  [3652275](https://www.pathofexile.com/forum/view-thread/3652275) (blind player asking for features, no staff answer seen) and
  [3407724](https://web.poecdn.com/forum/view-thread/3407724); GGG accessibility work found is colorblind options. The 2024 "accessibility macro ban" story was a hoax.

## 4. Steam: fetching an older exe (nothing downloaded)
- SteamDB returned HTTP 403 to WebFetch (`steamdb.info/app/2694490/depots/` and `/patchnotes/`), so depot IDs, manifest IDs and sizes are NOT verified here. The user must open
  https://steamdb.info/app/2694490/ in a browser, read the Windows depot's "Manifests" tab (manifest ID + date per build), and note the depot ID. Older manifests on SteamDB may need a logged-in account.
- Methods that are possible (documented behavior):
  1. Steam client console (`steam://open/console`): `download_depot <appid> <depotid> <manifestid>`; requires owning the game and being logged in; downloads the WHOLE depot to
     `steamapps/content/app_<appid>/depot_<depotid>/` (tens of GB for PoE2 - UNCERTAIN, check size in SteamDB). No file filter, so not the cheap option.
  2. [DepotDownloader](https://github.com/SteamRE/DepotDownloader) (3.4.0, 2025-05): `DepotDownloader -app 2694490 -depot <id> -manifest <id> -username <u> -remember-password -filelist files.txt -dir <out>`
     with `files.txt` containing `PathOfExileSteam.exe` (or `regex:` lines). Only chunks of the listed files are fetched, so the cost is about the exe size (a guess: 50-150 MB compressed; UNCERTAIN)
     and nothing is installed. `-manifest-only` writes the human-readable file list with sizes and hashes of the manifest without downloading files, which gives the exe's exact size and SHA for free.
     Requirements: .NET runtime, the owner's Steam login (2FA via `-qr` or code), network. Does not touch the installed game.
  3. Old manifests: Steam serves a manifest only while the depot's chunks still exist; whether GGG's older builds stay downloadable is UNCERTAIN (try the oldest manifest id first to find out).
- Do not use the beta-branch route unless SteamDB shows a public branch; none was verified.

## 5. Prior art for surviving binary updates
- Signature scanning with wildcards is the base technique everywhere: UE4SS ships per-engine-version AOBs under `assets/UE4SS_Signatures` and lets users override with Lua
  (`Register` returns the AOB, `OnMatchFound` fixes the address): [guide](https://docs.ue4ss.com/dev/guides/fixing-compatibility-problems.html). Its own guide calls finding new AOBs "quite complicated".
- [trumank/patternsleuth](https://github.com/trumank/patternsleuth) (pushed 2026-09-30): "a test suite for finding robust patterns ... in Unreal Engine games". You feed it many game executables and it
  scores resolvers, i.e. the multi-build testing we want (two exes to measure signature survival). Also uses string-reference resolvers. The tool is for UE; the method (run candidate signatures over N builds, keep those unique in all) transfers directly.
- UE4SS also reads the engine's own reflection (UObject/UProperty) and builds member offsets at run time; "UVTD" (Default_UVTD_Configs, VTableLayoutTemplates, MemberVarLayoutTemplates in UE4SS assets) uses PDB symbols to generate layouts. Applies only to engines with reflection or symbols.
- [REFramework](https://github.com/praydog/REFramework) (RE Engine, pushed 2026-11-04): leans on the engine's built-in type database / reflection to find fields by name at run time (README states AOBs may need updating, with a guide). Only a statement from memory about the TDB; not verified line-by-line here.
- Function matching across builds: BinDiff ([google/bindiff](https://github.com/google/bindiff), pushed 2026-09-18) and [Diaphora](https://github.com/joxeankoret/diaphora) (2026-10-25) match functions between two binaries (call graph, hashes, constants);
  FLIRT is library-signature matching in IDA. Ghidra has Version Tracking built in. Sigmaker plugins make wildcarded signatures ([vsaint1/sigmaker-ghidra](https://github.com/vsaint1/sigmaker-ghidra)). Not tested here.
- PoE2 has no known reflection or RTTI dump in public tools. UNCERTAIN whether PathOfExileSteam.exe has MSVC RTTI (vtable type names); our docs use only runtime vtable values. Checking with a quick scan for `.?AV` strings in the exe is cheap.

## Ideas worth trying (ordered)
1. Two-build signature test: fetch the old exe with DepotDownloader `-filelist` (item 4.2) and run candidate signatures from our own tooling over both exes (patternsleuth method). Measures survival before investing.
2. String-xref anchored location (upstream's approach): find a stable string ("Unable to get InGameState", "Mods.dat", ...) then the data reference from the function that uses it. Strings rarely change between builds,
   unlike instruction bytes. Gives the 6 global pointers without a pattern at all if the pattern fails (we already have `GameStatesSearch` fallback for one).
3. Check the exe for MSVC RTTI (`.?AVname@@`). If present, vtable-to-class-name mapping gives UI element classes and component types by name, no hand offsets.
4. Port the OffsetHelper anchor checks (upstream, `OffsetHelperEngine.cs`) into our healing as the generic last resort: any `[FieldOffset]` struct with an EntityPtr back-pointer, StdVector or Self pointer can be re-found by a +-0x200 scan. Mostly done for AreaInstance and vitals; extend to UI structs.
5. Do not expect help from dat-schema/RePoE for memory layout; use dat-schema only if we start reading dat rows (its poe2 JSON is fetchable and ETag-friendly).
6. Plan for 1.0 (2026-12-11, unverified): treat as a large break; keep `config/game-build.txt` detection and make the "game updated, found everything" message tell exactly which structs were healed vs. lost.

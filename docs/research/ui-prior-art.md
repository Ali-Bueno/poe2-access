# UI prior art and offline sources (web research 2026-09-14)

## PoE1 open-source offsets (do NOT reuse numerically — different struct/version)
- `vlaskinarita/GameOffsets` (current PoE1 ExileCore lineage) `ElementOffsets.cs`: SelfPointer +40, ChildStart +48,
  ScrollOffset +168, Position +192, Root +216, Scale +272, IsSelected +285, Parent/Type(ushort) +320,
  Flags(uint) +328 (`ElementFlags`: only IsScrollable 0x400 and IsVisibleLocal 0x800 named), Tooltip +344,
  Size +400, isHighlighted +456, ShinyHighlightState +515, **Text +760, TextNoTags +1056**.
- `IngameStateOffsets.cs`: UIHoverElement +1368, UIHover +1384, UIHoverTooltip, CurentUIElementPos +1376,
  UIHoverPos +1436. **No gamepad-focus field anywhere in PoE1 tools** — our `UiManager+0x988` finding is new.
- Older `Qvin0000/ExileApi`: `IsVisibleLocal` = byte bit 2; text via `EntityLabel.Text2` subclass.
- PoE1 wraps panels by fixed offsets into a root struct (`IngameUElementsOffsets.cs`), not by name lookup.
- Structural match with our PoE2 findings: flags bitfield + separate highlight/selected fields + Text/TextNoTags
  pair. Bit meanings are unpublished even for PoE1: our 0x0B/0x1B/0x10 are original, runtime-validated.

## Offline UI definitions
- PoE data bundles hold `Metadata/UI/*.ui` / `UISettings.xml` (per-state layout files; control names such as
  `login_button` are expected there — unverified until extracted).
- Steam install layout: `Bundles2\_.index.bin` (~115 MB) + `*.bundle.bin`; no `Content.ggpk`.
- Tools: `aianlinb/LibGGPK3` → `LibBundle3` (NuGet `LibGGPK3.LibBundle3`), needs a native `oo2core` (Oodle
  2.9.x) DLL supplied by the user; `OmegaK2/PyPoE` (Python). Issue LibGGPK3#25 tracks PoE2 quirks.

## Accessibility / controller docs
- No PoE2 screen-reader work exists. KB+M and controller render separate UI layouts. No official doc on
  focus traversal; derived empirically (focus-research.log).

# Delirium fog: the client's computation, for a port (PathOfExileSteam.exe 0.5.5f, static reading only)

Conventions. `u8/i8/u16/i32/u32/u64/f32` are exact widths. `R8<T>(a)` reads a T at address a. Every float operation is
IEEE single precision in the order written (the client uses SSE scalar ops; do not promote to double). Integer division
`/` on i32 truncates toward zero (the client uses the magic-number pattern `imul 0xB21642C9; sar 4; add sign`, which is C
truncation by 23). `S` = instance session, `R` = AfflictionEncounterRules (row 40) = `R8<u64>(S + 0x2540)`,
`M` = AfflictionMapRules (row 42) = `R8<u64>(S + 0x2550)`, `DM` = depth-map object (`R + 0xA0` for row 40, `M + 0x70` for
row 42; it is embedded in the ruleset, not a pointer). Addresses are image-relative (add the module base).

Floating constants used below (read from the exe): `K = 10.8695650100708f` (0x412DE9BD, grid to world),
`KINV = 0.0920000001788f` (0x3DBC6A7F), `250.0f`, `0.5f`, `INF = +inf` (0x7F800000), `2000.0f`, `4000.0f`, `0.00025f`.

---------------------------------------------------------------------------------------------------------------------

## 0. Objects and their layouts

```c
// Session and player (as 0x1803630 / 0x3B74E0 do it)
u64 playersBegin = R8<u64>(S + 0x48), playersEnd = R8<u64>(S + 0x50);
u64 player  = (playersBegin != playersEnd) ? R8<u64>(playersBegin) : 0;
u64 holder  = player ? R8<u64>(player + 0x188) : 0;
u64 entity  = holder ? R8<u64>(holder + 0x8) : 0;
u64 posObj  = R8<u64>(entity + 0x98);
i32 px = R8<i32>(posObj + 0x444), py = R8<i32>(posObj + 0x448);     // player grid position (cells)
u64 area = R8<u64>(S + 0x20F0);                                       // AreaInstance

// Depth map DM (constructor 0x1F65A50, destructor part 0x1802C40, build 0x1F65B20, packet 0x1F66A90)
//  DM+0x00 u64 vtable                (image 0x34FE0B8)
//  DM+0x08 u64 area                  (== R8<u64>(S+0x20F0))
//  DM+0x10 u64 H                     (heap object, 0x50 bytes, from 0x1F07F70(area))
//  DM+0x18 u64 regionsBegin          std::vector<RegionRecord>, 36-byte elements
//  DM+0x20 u64 regionsEnd
//  DM+0x28 u64 regionsCap
//  DM+0x30 u64 cellsBegin            std::vector<i8>, one region index per cell, w*h entries, row-major
//  DM+0x38 u64 cellsEnd
//  DM+0x40 u64 cellsCap
//  DM+0x48 u64 DMd                   heap object (distance grid), 0x50 bytes, polymorphic
//
// H = R8<u64>(DM+0x10):
//  H+0x00 u16 w        cells per row of the cell map (cells are half-tiles: 11.5 grid cells)
//  H+0x02 u16 h        rows
//  H+0x04 u16 ox       origin, in TILES (1 tile = 23 grid cells)
//  H+0x06 u16 oy
//  H+0x08 u64 walkableBegin   std::vector<u8>, w*h, nonzero = walkable (used by the build only)
//
// RegionRecord (36 bytes; the client receives all fields from the server, 0x1F66A90):
//  +0x00 i32 bboxMinX, +0x04 i32 bboxMinY     // UNSURE: meaning taken from the server-side build path (0x1F66561 copies
//  +0x08 i32 bboxMaxX, +0x0C i32 bboxMaxY     //   min/max+1 of the flooded nodes here); the client never reads them
//  +0x10 i32 seedX,    +0x14 i32 seedY        // grid cell the region's flood starts from (0x1F66780 collects these)
//  +0x18 8 bytes, always 0
//  +0x20 f32 baseDepth                        // +inf = region not connected to the fog origin
//
// DMd = R8<u64>(DM+0x48):
//  DMd+0x00 u64 vtable        (image 0x34FE048)
//  DMd+0x08 u64 dataBegin     std::vector<f32>: distance (grid cells) from the region seed, one per NODE
//  DMd+0x10 u64 dataEnd
//  DMd+0x18 u64 dataCap
//  DMd+0x20 u32 stride        = nodes per row = 2*areaWidthTiles + 1   (copied from DMd+0x48)
//  DMd+0x24 u32 nodeRows      = 2*areaHeightTiles + 1  (copied from DMd+0x4C)
// Nodes are ABSOLUTE half-tile points over the whole area: node (i,j) sits at grid ((23*i)>>1, (23*j)>>1).
// The cell map is RELATIVE to H's origin: cell (cx,cy) is node (cx + 2*ox, cy + 2*oy).
```

How the client fills them (for understanding; a reader does not redo it): the server sends the region records
(count = one u8, then per record three i32 pairs and one f32). The client then floods (0x1F65F90) from each record's seed,
in record order, over walkable cells with 8 neighbours, first visit wins (FIFO queue, no relaxation). The region index
written to the cell map is the flood order (an i8 counter; above 127 the client logs "[DELIRIUM] Too many areas while
decomposing navmesh"). The node distance is `parentDistance + sqrtf((f32)(dx*dx + dy*dy))`, where dx,dy are the i32
differences of the two nodes' grid positions `((23*c)>>1) + 23*origin`. The seed node keeps 0. A second pass floods every
remaining walkable cell as new regions. A final pass (0x1F65D4C) copies region index and node distance into every
non-walkable neighbour, so walls take their neighbour's values. The cell map starts at 0xFF (-1).

---------------------------------------------------------------------------------------------------------------------

## 1. PlayerDepth(x, y, max): exactly 0x1F66820 (called with DM, &{x,y}, max)

```c
// Depth unit: grid cells of approximate path distance from the fog origin.
// baseDepth is the server's depth of the region seed; the node values are chamfer path lengths in grid cells (0x16CB50 = sqrtf).
// max: row 40 passes i32 R8<i32>(R+0x128) (both 0x1803BBF and the progress bar 0x3B758D); row 42 passes R8<i32>(M+0xD0).

i32 PlayerDepth(u64 DM, i32 x, i32 y, i32 max)
{
    u64 H  = R8<u64>(DM + 0x10);
    u32 w  = (u32)R8<u16>(H + 0x0);
    u32 ox = (u32)R8<u16>(H + 0x4);
    u32 oy = (u32)R8<u16>(H + 0x6);

    // cell (nearest half-tile), signed i32 math, C truncating division by 23:
    i32 cy = (2 * (y - (i32)(23 * oy)) + 10) / 23;      // the code computes it as 2*(y - (23*oy - 5)) / 23
    i32 cx = (2 * (x - (i32)(23 * ox)) + 10) / 23;
    // The client does NOT bounds-check: it casts both to u16 and indexes.
    u64 idx = (u64)(u16)cx + (u64)w * (u64)(u16)cy;
    // PORT: check 0 <= cx < w and 0 <= cy < R8<u16>(H+0x2) and idx < (cellsEnd - cellsBegin); else return "unknown".
    // The client's own behaviour out of range is an out-of-bounds read (undefined), not a clamp or a default.

    i8 r = R8<i8>(R8<u64>(DM + 0x30) + idx);
    // The client does not test r. After the build, every cell reachable by the wall-dilation pass has r >= 0.
    // r < 0 means "never assigned" (0xFF initial value, e.g. before the first packet built the map).
    // PORT: r < 0, or r >= regionCount (= (regionsEnd - regionsBegin)/36) -> return "unknown".   // UNSURE: the client would read garbage here

    f32 base = R8<f32>(R8<u64>(DM + 0x18) + 36 * (i64)r + 0x20);

    f32 depth;
    if (base == INF)                      // ucomiss; equal only for exactly +inf
        depth = INF;                      // the client skips the bilinear part
    else
        depth = Bilinear(R8<u64>(DM + 0x48) + 8, x, y) + base;   // addss: bilinear + base

    if (depth == INF)
        return 8000;                      // also logs "[DELIRIUM] Attempting to get depth for location in unconnected area"

    f32 c = CeilF(depth);                 // 0x19FC50, see below
    i32 v = CvttToI32(c);                 // cvttss2si: truncation; NaN or out of range -> INT32_MIN (0x80000000)
    if (max > 0) {
        i32 lo = (v >= 1) ? v : 1;        // cmovge
        return (max >= v) ? lo : max;     // cmovge on (max >= v); i.e. clamp to [1, max]
    } else {
        return (v > 1) ? v : 1;           // cmovg: only a lower clamp when max <= 0
    }
}

// 0x19FC50: ceiling that stays in float
f32 CeilF(f32 a) {
    i32 t = CvttToI32(a);
    if (t == INT32_MIN) return a;                  // overflow or NaN: unchanged
    if ((f32)t == a) return a;                     // already integral
    return (f32)(t + (SignBit(a) ? 0 : 1));        // positive non-integer: +1; negative: trunc (toward zero) = ceil
}

// 0x1F63FA0, G = DMd + 8 (so G+0 = dataBegin, G+0x18 = DMd+0x20 = stride)
f32 Bilinear(u64 G, i32 x, i32 y)
{
    // grid -> world (0x21A3430): (cell + 0.5) * K, float
    f32 Wx = ((f32)x + 0.5f) * K;
    f32 Wy = ((f32)y + 0.5f) * K;
    // world -> grid (0x21A3470): floor(W * KINV) as i32 (0x168860 = floorf). Equals x,y in practice; compute it anyway.
    i32 gx = CvttToI32(FloorF(Wx * KINV));
    i32 gy = CvttToI32(FloorF(Wy * KINV));

    u32 stride = R8<u32>(G + 0x18);                    // = R8<u32>(DMd + 0x20)
    u64 data   = R8<u64>(G + 0x0);                     // = R8<u64>(DMd + 0x08)
    i32 i = (2 * gx) / 23;                             // signed, truncating
    i32 j = (2 * gy) / 23;
    // indices are 64-bit: (i64)row * (u64)stride + (i64)col; no bounds check in the client.
    f32 v00 = R8<f32>(data + 4 * ((i64)j       * stride + (i64)i));
    f32 v10 = R8<f32>(data + 4 * ((i64)j       * stride + (i64)(i + 1)));
    f32 v01 = R8<f32>(data + 4 * ((i64)(j + 1) * stride + (i64)i));
    f32 v11 = R8<f32>(data + 4 * ((i64)(j + 1) * stride + (i64)(i + 1)));
    // PORT: check (j+1)*stride + i+1 < (dataEnd - dataBegin)/4 and i+1 < stride; else "unknown".

    // node (i,j) world position: grid ((23*i)>>1, (23*j)>>1), the shift is a logical shift of the 64-bit product
    i32 nx = (i32)(((u64)(i64)i * 23) >> 1);
    i32 ny = (i32)(((u64)(i64)j * 23) >> 1);
    f32 NWx = ((f32)nx + 0.5f) * K;
    f32 NWy = ((f32)ny + 0.5f) * K;

    f32 fx = (Wx - NWx);  fx = fx + fx;  fx = fx / 250.0f;   // fraction inside the node cell, ~[0, 1.04]
    f32 fy = (Wy - NWy);  fy = fy + fy;  fy = fy / 250.0f;

    f32 top    = (v10 - v00) * fx + v00;      // order as in the code: subss, mulss, addss
    f32 bottom = (v11 - v01) * fx + v01;
    return (bottom - top) * fy + top;
}
```

`CannotBeAfflictedAt(area, x, y)` used below is `0x1E84CF0(area, 0x2A28, &{x,y})`. Stat key 0x2A28 is
`cannot_be_afflicted`; the function returns that stat's value for the area, looked up per tile.
// UNSURE: its exact semantics (area stat plus per-tile zones). It is not ported here: treat it as 0 except in towns or hideouts.

Who else calls it: the row-40 tick 0x1803BD2 (for visual density) and the progress bar 0x3B761A / 0x3B76E8.

---------------------------------------------------------------------------------------------------------------------

## 2. Front(R) and Rear(R): exactly 0x1F66EF0 and 0x1F66F60 (argument FB = R + 0x58)

Field map (FB offset = R offset − 0x58):

| R offset | FB | type | meaning | written by |
|---|---|---|---|---|
| R+0x58 | +0x00 | u64 P | AfflictionBalancePerLevel row (section 4) | 0x1F66C40 at construction |
| R+0x60 | +0x08 | u64 | P's table handle | same |
| R+0x70 | +0x18 | f32 | rear acceleration multiplier = 1 + stat `map_affliction_secondary_wave_acceleration_+%` / 100 | 0x1F66E5E, construction |
| R+0x74 | +0x1C | f32 | extra rear delay, s = stat `…secondary_wave_delay_ms_+` / 1000 + stat `…secondary_wave_delay_seconds_+` | 0x1F66E97 |
| R+0x78 | +0x20 | f32 | t, front clock, s (−1.8 at construction) | packet 0x1803E05; tick 0x1803A7D |
| R+0x7C | +0x24 | f32 | t2, rear clock, s (−1.8 at construction) | packet 0x1803E2B; tick 0x1803ABB |
| R+0x84 | +0x2C | f32 | end time on the t2 clock, s; +inf while running | packet 0x1803E51 |
| R+0x88 | +0x30 | f32 | pause seconds left | packets 0x1803E7A, 0x180425D; tick 0x1803AAC |
| R+0x8C | +0x34 | u8 | active | packet |
| R+0x8D | +0x35 | u8 | finished | packet |
| R+0x8E | +0x36 | u8 | never dissipates (map stat `map_delirium_fog_never_dissipates`) | construction 0x1F66ED3 |
| R+0x128 | | i32 | max depth; 8000 at construction | packets 0x1803F63, 0x1804145 |
| R+0x130 | | u8 | pause hold (rear clock frozen) | packets 0x1803EA4, 0x1804287 |

Units: t, t2, T, delays in seconds (the delay stat is divided by 1000.0f). v0, v1, V in grid cells per second. A in cells/s².
Front and Rear are in the same depth unit as PlayerDepth (grid cells of path distance).

```c
f32 Front(u64 R)                                   // 0x1F66EF0
{
    f32 t = R8<f32>(R + 0x78);
    if (0.0f > t) return 0.0f;                     // before the start (t starts at -1.8); NaN t falls through
    u64 P  = R8<u64>(R + 0x58);
    f32 T  = R8<f32>(P + 0x34);
    f32 v1 = R8<f32>(P + 0x04);
    f32 v0 = R8<f32>(P + 0x38);
    f32 h  = ((v1 - v0) / T) * 0.5f;               // half acceleration (negative with shipped data)
    if (t > T)                                     // strictly greater; t == T takes the second branch
        return ((h * T) * T + v0 * T) + (t - T) * v1;
    else
        return (h * t) * t + v0 * t;
}

f32 Rear(u64 R)                                    // 0x1F66F60
{
    if (R8<u8>(R + 0x8E) != 0) return 0.0f;        // never dissipates
    u64 P     = R8<u64>(R + 0x58);
    f32 end   = R8<f32>(R + 0x84);
    f32 delay = R8<f32>(R + 0x74) + R8<f32>(P + 0x0C);   // extra delay + row delay
    f32 m     = (delay < end) ? delay : end;       // 0x1528A0 min: returns delay only if delay < end
    f32 s     = R8<f32>(R + 0x7C) - m;             // time the rear has been moving
    if (0.0f > s) return 0.0f;                     // not started yet
    f32 A = R8<f32>(P + 0x10) * R8<f32>(R + 0x70); // acceleration
    f32 V = R8<f32>(P + 0x08);                     // start speed
    f32 e = end - m;                               // time the rear moved before the end; +inf while running
    f32 halfA = A * 0.5f;
    f32 rear;
    if (s > e) {                                   // only after the end time (end finite)
        f32 d   = s - e;                           // seconds since the end
        rear    = (halfA * e) * e + e * V;         // distance covered until the end
        rear    = rear + (e * A + V) * d;          // speed at the end, kept
        f32 a2h = (A * 2000.0f) * 0.5f;            // after the end the acceleration is 2000*A
        rear    = rear + (a2h * d) * d;
    } else {
        rear = (halfA * s) * s + V * s;
    }
    f32 front = Front(R);
    return (front < rear) ? front : rear;          // 0x1528A0: the rear never passes the front
}
```

Clamp order:
- Front/Rear return unclamped floats. The shader path (0x1803B62..0x1803BB1) subtracts the player's region base and then
  applies max(0, ·).
- The progress bar uses the integer getters. 0x1802CE0 returns `min((i32)trunc(Front(R)), R8<i32>(R+0x128))`.
  0x1802CB0 returns `min((i32)trunc(Rear(R)), R8<i32>(R+0x128))`.
- There is no lower clamp in the getters; both values are >= 0 anyway. `cvttss2si` gives INT32_MIN on overflow or NaN, and
  the `cmovl` then keeps INT32_MIN.

Before start / after end:
- Before the start, t < 0 and t2 < delay, so both return 0.
- While running, end = +inf, so e = +inf, and Rear uses the first-phase formula.
- After the end, if the rear had started (end > delay), it continues from its state at the end with 2000x acceleration.
  It is clamped to Front, so the band collapses.
- If the encounter ended before the rear started (end <= delay), then m = end and e = 0, so
  `rear = V*d + 1000*A*d²` with d = t2 - end.

Who advances the clocks (0x1803630, every frame, dt in seconds):
- Only when R+0x8C == 1, R+0x8D == 0, R+0x84 == +inf, the player cell is not `cannot_be_afflicted` (0x1E84CF0(area, stat
  0x2A28, &pos) == 0), and `R8<u8>(area + 0x890) == 0` (// UNSURE: what that area byte means, probably "area clock frozen").
- When these hold, t += dt.
- Then, if R+0x130 != 0, t2 stays. Otherwise, if R+0x88 > 0, R+0x88 -= dt (it can go slightly negative). Otherwise t2 += dt.
- In all other cases the client leaves both clocks alone until the next packet.
- A reader must not integrate: re-read R+0x78 and R+0x7C every time.

The paused flag shown by the bar (0x1802D10): `R8<u8>(R+0x130) != 0 || R8<f32>(R+0x88) > 0.0f`.

---------------------------------------------------------------------------------------------------------------------

## 3. The progress bar, 0x3B74E0 (what it shows and from where)

```c
// session from the UI: S = R8<u64>(R8<u64>(widget+0x228) + 0x1A8); the order of tests is the code's.
u64 R = R8<u64>(S + 0x2540);                 // row 40 AfflictionEncounterRules
if (R != 0) {
    if (R8<u8>(R + 0x8C) == 0) return;       // NO bar, and it does NOT fall back to row 42
    i32 maxI   = R8<i32>(R + 0x128);
    f32 maxF   = (f32)maxI;                  // cvtdq2ps (signed)
    f32 inv    = 1.0f / maxF;
    f32 rearF  = (f32)Getter_Rear(R)  * inv; // 0x1802CB0, i32
    f32 frontF = (f32)Getter_Front(R) * inv; // 0x1802CE0, i32
    i32 depth  = player ? PlayerDepth(R + 0xA0, px, py, maxI) : 0;   // no cannot_be_afflicted test on this path
    bool paused = Paused(R);                 // 0x1802D10
    bool mapStyle = false;
} else {
    u64 M = R8<u64>(S + 0x2550);             // row 42 AfflictionMapRules
    if (M == 0) return;
    f32 maxF   = (f32)R8<i32>(M + 0xD0);
    f32 rearF  = 0.0f;
    f32 frontF = (f32)R8<u32>(M + 0xD4) * 0.00025f;   // u32 (mov eax; cvtsi2ss rax), i.e. /4000
    u32 depth  = (player && CannotBeAfflictedAt(area, px, py) == 0)
                 ? (u32)PlayerDepth(M + 0x70, px, py, R8<i32>(M + 0xD0)) : 0;
    bool paused = false;
    bool mapStyle = true;                    // shows the label (i32)(frontF * 100.0f) (truncation)
}
if (!(maxF > 0.0f)) return;                  // comiss maxF, 0 ; jbe -> no bar
f32 playerF = (f32)depth / maxF;             // 0x3B78A8, both rows
// bar fill = [rearF, frontF], player marker at playerF, "paused" art when paused
```

Row 41 (AfflictionEndGameRules, 0x88 bytes) is not read by the bar and has no depth map. Its packet (0x18049D0) carries
only +0x78, +0x7C (i32), +0x80, +0x81 (u8).

Row 42 fields, all from the packet 0x180D1D0:
- M+0xD0: i32 max depth.
- M+0xD4: i32 = 40 × stat key 0x2A1F `map_endgame_fog_depth` (at full sync), or 40 × a packet i32 (type 1).
- M+0x60: i32 x,y grid point.
- M+0xD8 / +0xDC: i32 origin grid x,y.
- M+0xC0: f32 x3 world centre of +0xD8.
- M+0xCC: f32 radius. It is K × the largest distance from M+0x60 to the area's four corners, so the circle covers the whole
  map.
- M+0x70: the depth map (layout as in section 0).

The map shader (0x180D0E0) draws inner radius 0 and outer radius M+0xCC from centre M+0xC0 whenever the player cell is not
`cannot_be_afflicted`. So in a row-42 area the fog is drawn everywhere, with no clocks, no front and no rear.

Which ruleset a MAP encounter uses:
- The client code alone does not say; the server creates the rulesets.
- The bar prefers row 40 whenever it exists, so a map that carries a mirror encounter as row 40 behaves like the campaign
  (band, clocks).
- Row 42 is the "endgame" variant: its fill reads `map_endgame_fog_depth`, and it has no clocks.
- // UNSURE: whether ordinary endgame maps with Delirium instantiate row 40, row 42, or both. Log both pointers in a map.
- // UNSURE: for row 42 the code only shows frontF (fog depth %) and playerF on one bar. "Inside" is not computed on the
  client; my reading is that the whole map counts as fog (except `cannot_be_afflicted` ground) and the bar is reward depth,
  not a boundary.

---------------------------------------------------------------------------------------------------------------------

## 4. AfflictionBalancePerLevel row, as the code uses it

The row in memory has the file layout: 0x50 bytes, iterated by 0x50 in 0x1F66C40; every field below is a plain i32/f32, no
pointers. The values in the table are from `data_balance_afflictionbalanceperlevel.datc64`.

| offset | type | meaning (used by) | row0 (lvl 1) | row1 (lvl 65) | row2 (lvl 101) | row3 (lvl 201) |
|---|---|---|---|---|---|---|
| +0x00 | i32 | minimum area level | 1 | 65 | 101 | 201 |
| +0x04 | f32 | v1, front speed after the ramp (Front) | 30 | 30 | 0 | 50 |
| +0x08 | f32 | V, rear start speed (Rear) | 1 | 5 | 0 | 200 |
| +0x0C | f32 | rear delay, s (Rear) | 10 | 15 | 0 | 4 |
| +0x10 | f32 | rear acceleration (Rear, × R+0x70) | 0.38 | 0.05 | 0 | 10 |
| +0x34 | f32 | T, front ramp duration, s (Front) | 100 | 100 | 0 | 4 |
| +0x38 | f32 | v0, front start speed (Front) | 150 | 150 | 0 | 200 |

Other columns (+0x1C..+0x30, +0x3C..+0x4C ints such as 11, 33, 100, 4000, 20, 80) are not read by these functions.
// UNSURE: their meaning.

Choice of P (0x1F66C40, once, at the ruleset's construction):
- areaLevel = `R8<u8>(area + 0xC4)`.
- Walk the rows in table order; while `row.level <= areaLevel`, P = row; stop at the first row with level > areaLevel.
- If the first row already exceeds the level, P stays 0.
- A reader just reads `P = R8<u64>(R + 0x58)`.

Row 2 has all speeds 0. With it, T = 0 and Front computes (0/0)*0.5 = NaN, the same as the game. Area levels of 101–200 are
not expected in play.

---------------------------------------------------------------------------------------------------------------------

## 5. Run-time sanity checks, and the cross-check

Per step:
1. `R != 0`, `R8<u64>(R+0x48) == S` (every ruleset points back to its session), `R8<u8>(R+0x8C)` and `R8<u8>(R+0x8D)` are 0 or 1.
2. `P = R8<u64>(R+0x58) != 0`, `R8<i32>(P) <= areaLevel`, all six floats finite, `T > 0`, `v0 > 0`, `v1 > 0`,
   `V, delay, accel >= 0`. With shipped data, {150, 30, 100} for the front.
3. `R+0x70` is finite and about 0.5–5. `R+0x74` is >= 0 and below about 60.
4. `t = R+0x78`: >= -1.8, rises by about 1 per second while the encounter is active. `t2 = R+0x7C <= t + 0.5`.
   `R+0x84` is +inf while `R+0x8D == 0`. `R+0x88 >= -0.1`.
5. `R+0x128`: 8000 before the first packet (treat as "not ready"), else 1 .. about 20000.
   `0 <= rearI <= frontI <= R+0x128`.
6. DM:
   - `R8<u64>(DM) == base + 0x34FE0B8`, `R8<u64>(DM+8) == area`, H != 0, w and h in 1..4096.
   - `cellsEnd - cellsBegin == w*h`, `(regionsEnd - regionsBegin) % 36 == 0`, region count 1..127.
   - Each record's seed lies inside the area. `baseDepth` is >= 0 and finite, or +inf.
7. DMd:
   - `R8<u64>(DMd) == base + 0x34FE048`.
   - `stride == 2*areaWidthTiles + 1`; check `stride * nodeRows == (dataEnd - dataBegin)/4`.
   - Node values >= 0 and finite.
8. Player depth: 1..max, or 8000 (unconnected). It changes continuously, by at most about 1.5 × the grid distance walked.
   Within the band it should rise as you walk away from the origin.
9. Bar fractions in [0,1]; `rearF <= frontF`.

Best single cross-check with the server: the player stat key 10259 `is_in_affliction_fog` (exe reads it at 0x12FD33 and
0x16C1331).
- For row 40 compute `inFog = (Rear(R) < depth) && (depth <= Front(R))`, using the clamped integer getters, as the bar does.
- Sample it and the stat once per second during an encounter.
- They should agree whenever the player is more than about 10 cells from either edge (`depth - rearI > 10` and
  `frontI - depth > 10`) and the stat has not changed in the last second.
- Count the mismatches outside that margin. Zero or near-zero confirms the port; a systematic offset means the depth or one
  edge is wrong.
- // UNSURE: whether the server uses < or <= at the edges, and whether it applies its own margin. The stat is computed on the
  server and not visible here.
- For row 42 the stat should be 1 nearly everywhere in the area. Treat that as the test of the "whole map is fog" reading
  above (UNSURE).

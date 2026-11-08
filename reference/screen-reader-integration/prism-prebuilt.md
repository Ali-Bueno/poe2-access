# PRISM — consuming the prebuilt release (verified, DO NOT re-investigate)

> **Read this when:** you're wiring PRISM into a mod for the first time (any engine) and need the
> concrete facts about the release layout, exports, call sequence and what to ship. One-time-per-mod
> reference — not needed once the adapter is working. For *how the call physically reaches PRISM on
> each engine* (the transport), see [the integration index](README.md).
>
> Checked against **PRISM v0.16.7** by downloading the asset and reading the binary/headers;
> release layout re-verified against **v0.17.3** (2026-07-20) and **v0.18.2** (2026-09-14; the zip's
> `include/` now also ships `prism_version.h`, which `prism.h` includes — copy both) and **v0.18.3**
> (2026-11-08, D2R and PoE2 mods: Wine bridges, below; `PrismConfig` grew a field, `PRISM_CONFIG_VERSION` 4, so
> rebuild against the headers of the `prism.dll` you ship and take the config from its `prism_config_init`).
> **No need to compile the PRISM repo or use CMake/vcpkg** to consume it: the release is enough.

- **Per-platform releases** on GitHub (`ethindp/prism`, assets of the latest release). For Windows:
  **`prism-windows-x64.zip`** and `prism-windows-arm64.zip`. **There is no x86 (32-bit) build** → if the
  game/host is 32-bit, the PRISM prebuilt is NOT usable (you'd have to compile). Most modern x64 games →
  use `x64`.
- **Stable C API**: `include/prism.h` is wrapped in `extern "C"`, pure C types (`char*`, `float`,
  `size_t`, opaque pointers `PrismContext*`/`PrismBackend*`), `__cdecl` convention, `PRISM_API` macro
  (`__declspec(dllimport/dllexport)`). **No C++ type crosses the boundary** → a DLL built with **MinGW or
  MSVC** can consume `prism.dll` (which is built with MSVC) **with no ABI problems**.
- On **x64 the exports are NOT decorated** (`prism_init`, `prism_backend_speak`, …) → the most robust
  path is **dynamic loading**: `LoadLibrary("prism.dll")` + `GetProcAddress` by name. Zero link config,
  zero import lib, and you control when it loads (key if it's an injected DLL: load outside `DllMain`).
- **Windows zip contents**: `include/prism.h`; `dynamic/release/bin/prism.dll` (~1.1 MB, **the ONLY
  file you distribute**) + `tolk.dll` (~525 KB, optional Tolk-compat backend — do **not** ship it:
  `prism.dll` does not import it, and NVDA/JAWS/SAPI support is built in); `dynamic/release/lib/prism.lib`
  (MSVC import lib, optional); a CMake package (target **`prism::prism`**). It also includes `static/`
  with a ~79 MB `prism.lib` → **it's MSVC C++: NOT linkable from MinGW** (MSVC only, yields a single
  self-contained DLL).
- **`prism.dll` runtime dependencies**: only system DLLs + UCRT (`KERNEL32`, `USER32`, `ole32`,
  `OLEAUT32`, `RPCRT4`, `UIAutomationCore`, `api-ms-win-core-*`). **Does NOT depend on
  `vcruntime140`/`msvcp140`** → **no VC++ Redistributable required**. Asian-reader backends
  (`PCTKUSR.dll`, `ZDSRAPI_x64.dll`) load **on demand**; for NVDA/JAWS/SAPI **you don't have to
  distribute them**. The Wine bridges are the exception: see the next bullet.
- **Wine / Proton (Linux players) — PRISM ≥ v0.18.3 plus the bridge payload.** In a Proton prefix there
  is no NVDA/JAWS, OneCore is not implemented and SAPI usually has no voices, so without bridges
  `acquire_best` returns nothing. An NVDA stand-in (Cthulhu's Wine Access) does not help since v0.16.2:
  the NVDA backend also calls `RpcMgmtInqIfIds`, a stub that always fails in Wine (PRISM ≤ v0.12.2 still
  speaks through it). Windows builds before v0.18.3 left the bridged backends (Orca, Speech
  Dispatcher, Spiel) out of the registry entirely (prism#126 fixed it). The payload is in
  **`prism-linux-x64.zip`**, not the Windows zip: `dynamic/release/wine/placeholders/prism_{orca,speech_dispatcher,spiel}_bridge.dll`
  (2 KB PE placeholders) and `dynamic/release/wine/*.dll.so` (the ELF halves). Same release as `prism.dll`,
  never renamed. A placeholder is looked for on the DLL search path, then in **`prism.dll`'s own folder**
  (`source/optional_library.cpp`), so ship the three placeholders beside `prism.dll`; the `.dll.so` files go in a
  `wine/` folder that must be in **`WINEDLLPATH` when the game process starts** (Wine reads it at start; a mod
  cannot set it from inside). Bridges load only under Wine (`wine_get_version` check), so they are inert on
  Windows. Upstream manual: `doc/src/wine-bridges.md`. Worked examples: the D2R mod's `docs/proton.md`, the PoE2
  mod's `docs/proton.md`. A self-updating mod must not overwrite a mapped `.dll.so` in place: write the new file
  beside it and move it over.
- **Minimal sequence**: `prism_config_init` → `prism_init(&cfg)` → `prism_registry_acquire_best(ctx)` →
  `prism_backend_initialize(be)` → `prism_backend_speak(be, txt, interrupt)` → … → `prism_backend_free` +
  `prism_shutdown`. **v0.18.x (verified 2026-09-14 on v0.18.2, D2R mod):** `acquire_best` returns a *shared,
  already-initialized* backend, so `prism_backend_initialize` answers `PRISM_ERROR_ALREADY_INITIALIZED` —
  treat that code as success (only other errors are failures), otherwise the adapter wrongly reports
  "backend NVDA failed to initialize" and stays mute. Use `interrupt=true` when switching context; log the chosen backend with
  `prism_registry_name`. Others: `prism_backend_braille`, `prism_backend_is_speaking`, `set_rate/volume/pitch`.
- **Static linking (source-level ports only)**: injected/plugin hosts dynamic-load `prism.dll`, but a mod
  compiled *into* the game (decomp / libultraship PC ports) can link `prism.lib` and call the API directly.
  The static `prism.lib` is **MSVC C++** → MSVC toolchain only (not MinGW). Still copy `prism.dll` next to the exe as a POST_BUILD step.
- **Version note — use PRISM ≥ v0.17.0.** Releases v0.16.2–v0.16.7 had a regression
  ([issue #49](https://github.com/ethindp/prism/issues/49)) where the **NVDA backend's `initialize()`
  falsely succeeded when NVDA wasn't running, shadowing JAWS/other readers** — so `acquire_best` could
  pick a dead backend. **Fixed in v0.17.0** (2026-07-07); `acquire_best` is correct there. Only if you're
  pinned to an affected version, the workaround is to enumerate the registry and pick the highest-priority
  backend reporting `IS_SUPPORTED_AT_RUNTIME` instead of `acquire_best` — and retire that workaround once
  you upgrade to ≥ v0.17.0.
- **Official bindings — "Prismatoid"** (an in-progress effort upstream): **.NET** — `Prismatoid` on
  NuGet (v0.3.0 as of 2026-07, MPL-2.0) targets **.NET 10** → fine for standalone tools, NOT loadable
  inside BepInEx (net472/net6) or other injected .NET hosts, which keep the P/Invoke dynamic-load path
  above; **Python** — `Prismatoid` on PyPI (tooling/prototyping); **Godot** — in-tree in the prism
  repo. The C API above remains the universal path for injected mods. **Prismatoid 0.4.0 (2026-08, the newest
  as of 2026-11) carries PRISM 0.17.3 — no Wine bridges — and allocates a 48-byte `PrismConfig`, while 0.18.3's
  `prism_config_init` writes 56: do not pair it with a 0.18.3 `prism.dll`.** The PoE2 mod (.NET 10) replaced it
  with its own small binding (`PrismLibrary.cs`: `LoadLibrary` + function pointers, the config matched to the
  header and filled into a larger slot); d4-assist kept Prismatoid 0.3.0 with `ExcludeAssets="native"`.
- **Deploy**: copy `prism.dll` next to the mod DLL (`tolk.dll` from the zip is NOT needed), plus the
  Wine bridge payload above if Linux players should hear it.

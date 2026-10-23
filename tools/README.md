# Tools

Research tooling that has to be re-run after every game patch. Nothing here ships with the plugin.

## Poe2UiExtract
.NET 10 console app that opens the Steam install's `Bundles2\_.index.bin` with `LibGGPK3.LibBundle3` and
extracts every `metadata/ui/**` script (`.ui`, `.xml`, ...) into an output folder. Read-only on the game.

```
dotnet build tools/Poe2UiExtract -c Release
tools\Poe2UiExtract\bin\Release\net10.0\Poe2UiExtract.exe "D:\games\steam\steamapps\common\Path of Exile 2\Bundles2\_.index.bin" external\poe2-ui
```
Needs `oo2core.dll` next to the exe (see below). Output lives in `external/poe2-ui/` (gitignored: game data).

## ooz-shim — Oodle-compatible `oo2core.dll`
PoE2 links Oodle statically, so no `oo2core_9_win64.dll` is available. `oodle_shim.cpp` re-exports
`OodleLZ_Decompress` (the signature LibBundle3 P/Invokes) on top of the open-source decompressor
https://github.com/zao/ooz (`Ooz_Decompress` already has the same 14-parameter signature; codec is read from the
stream header so Kraken/Mermaid/Leviathan all work).

Build (VS 2022 Build Tools + CMake + Ninja):
```
git clone --recursive https://github.com/zao/ooz
copy tools\ooz-shim\oodle_shim.cpp ooz\
# CMakeLists.txt: add oodle_shim.cpp to libooz sources and set_target_properties(libooz PROPERTIES OUTPUT_NAME "oo2core")
call "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
cmake -S ooz -B ooz\build -G Ninja -DCMAKE_BUILD_TYPE=Release -DOOZ_BUILD_EXE=OFF -DOOZ_BUILD_BUN=OFF -DOOZ_BUILD_VALIDATE=OFF
cmake --build ooz\build --target libooz --config Release
```
The built binary is kept at `external/oodle/oo2core.dll` (gitignored). Decompression only; compression exports are stubs.

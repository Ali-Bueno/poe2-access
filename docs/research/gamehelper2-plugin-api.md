# GameHelper2 — plugin/host contract (clone 2026-09-14, v2.7.x)

## Build / layout
- `net10.0-windows`, `win-x64`, .NET 10 SDK required (`README.md:14-26`). Build the whole solution
  (`dotnet build GameOverlay.sln -c Release`) — plugins have post-build Copy steps (`README.md:98-104`).
- Output: `GameHelper\bin\<Config>\net10.0-windows\win-x64\`; run `Launcher.exe` (elevated if the game is).
- Plugins: `...\win-x64\Plugins\<Name>\<Name>*.dll` — dll name must start with the folder name (`PManager.cs:186`).
- Config: `configs\core_settings.json`, `configs\plugins.json`, `Plugins\<Name>\config\`.

## Plugin class (`GameHelper/Plugin/PCore.cs:15-64`)
`public abstract class PCore<TSettings> : IPCore where TSettings : IPSettings, new()`
- fields: `string DllDirectory`, `TSettings Settings`; `protected PluginLocalization PluginText`.
- abstract: `OnEnable(bool isGameOpened)`, `OnDisable()`, `DrawSettings()`, `DrawUI()`, `SaveSettings()`.
- virtual: `GetDescription()`, `ConflictsWith`, `ConflictPriority`.
- Exactly ONE `sealed` class implementing `IPCore` per assembly (`PManager.cs:233-242`).
- Own collectible `AssemblyLoadContext` per plugin; `GameHelper.dll` and `GameOffsets.dll` are shared, everything
  else resolved from the plugin's deps (`Plugin/PluginAssemblyLoadContext.cs:11-33`).
- No load-settings hook: load in `OnEnable`. Host calls `SaveSettings()` periodically for enabled plugins only.
- `DrawUI()` = once per render frame (`PManager.cs:614-651`), exceptions caught per plugin. No separate tick:
  do logic in `DrawUI` or start a coroutine `CoroutineHandler.Start(...)` waiting on public events.
- HUD drawing should use `ImGui.GetBackgroundDrawList` (`IPCore.cs:57-59`).

## Template csproj (`Plugins/SamplePluginTemplate/Changeme/Changeme.csproj`)
Library, `net10.0-windows`, x64, `ProjectReference ..\..\GameHelper\GameHelper.csproj` with `<Private>false</Private>`,
`PackageReference ImGui.NET` (template pins 1.90.0.1; main app 1.91.6.1 — match the app), post-build Copy to
`..\..\GameHelper\$(OutDir)Plugins\$(ProjectName)`. **Template bug:** `ChangemeCore.cs:30` overrides `OnEnable()`
without the bool parameter — won't compile; use `OnEnable(bool isGameOpened)`.

## State / events
- `Core.States` (`GameStates`), `Core.Process` (`Pid`, `Foreground` public; `Address` internal), `Core.GHSettings`.
- `GameStateTypes` (`RemoteEnums/GameStateTypes.cs`): AreaLoadingState, ChangePasswordState, CreditsState,
  EscapeState, InGameState, PreGameState, LoginState, WaitingState, CreateCharacterState, SelectCharacterState,
  DeleteCharacterState, LoadingState, UnknownState, GameNotLoaded.
- `Core.States.GameCurrentState` — **poll it**; `RemoteEvents.StateChanged` is internal (`CoroutineEvents/RemoteEvents.cs:26`).
- Public events: `RemoteEvents.AreaChanged` (public), `GameHelperEvents.OnMoved/OnForegroundChanged/OnClose`.
  Pattern: `yield return new Wait(RemoteEvents.AreaChanged);` (`Plugins/AutoHotKeyTrigger/AutoHotKeyTriggerCore.cs:416,546`).
- Typed state objects: only `AreaLoadingState` (`CurrentAreaName`) and `InGameState`
  (`CurrentWorldInstance`, `CurrentAreaInstance`, `GameUi`, `MouseOverEntity`).

## Hotkeys
`ClickableTransparentOverlay.Win32.Utils.IsKeyPressedAndNotTimeout(VK key, int timeoutMs)` (debounced) and
`Utils.IsKeyPressed(VK)`; backed by Win32 `GetKeyState`, works while the GAME window has focus (this is how
AutoHotKeyTrigger fires). `VK` enum from `ClickableTransparentOverlay.Win32`. Settings store keys as `VK` fields.

## Logging
No logger: convention is `Console.WriteLine($"[Plugin.Method] ...")`. Unhandled exceptions → `Error.log`.

## Shipped plugins
Atlas2, AutoHotKeyTrigger, HealthBars, LootValue, PickupHelper, PlayerBuffBar, PreloadAlert, Radar,
RitualWispAlert, WorldDrawing (demo), SamplePluginTemplate. No speech/TTS code anywhere.

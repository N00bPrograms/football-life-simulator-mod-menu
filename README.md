# Football Life Simulator Mod Menu

An in-game cheat menu for **Football Life Simulator** (Steam app 3158270), built as a
[MelonLoader](https://github.com/LavaGang/MelonLoader) mod. Press **F8** in game.

> Unofficial fan mod, not affiliated with Ludu. Single-player. Back up your save before heavy editing:
> `%USERPROFILE%\AppData\LocalLow\Ludu\Football Life Simulator\<SteamID>\Save_0`

## Features

| Tab | What it does |
|---|---|
| Money | Set money, +1k / +10k / +100k / +1M, lock money so it never drops. Transactions show in the bank app as "Mod Menu". |
| Needs | Energy, Hunger, Health, Mood, Stamina, Toilet, Match condition: −/+ each, Max each, Max all, and "Keep all needs full". |
| Skills | Agility, Dribbling, Fitness, Positioning, Power, Shooting, Technique, plus Charisma, Fame and Flirty: −/+, Max, Max all. |
| Time | Pause/resume the clock, skip +1 hour / +6 hours / +1 day, clock speed ×0.5 to ×10. |

All needs drain toward 0 over time, so **Max = fully satisfied** (Toilet 100 = no need to go).
Fame's cap is very high (around 62,000), so "Max all" in the Social group makes you very famous.

## Install

1. Install **MelonLoader 0.7.3** into the game ([installer](https://github.com/LavaGang/MelonLoader/releases)).
2. Start the game once so MelonLoader generates its files, then close it.
3. Copy `FLSModMenu.dll` into the game's `Mods` folder.
4. Start the game, load a save, press **F8**.

The hotkey can be changed in `UserData\MelonPreferences.cfg` under `[FLSModMenu] ToggleKey`
(any Unity Input System key name: `F8`, `F1`, `Insert`, ...).

## How it works

- **Finding the game's systems:** the game wires its services with VContainer, but they
  aren't resolvable from the project scope. The mod uses Harmony postfixes on methods
  the HUD calls every frame (`FinanceManager.CurrentMoney`, `TimeManager.GetTime`,
  `PlayerStatManager.GetOverall`, ...) to capture the live instances.
- **Stats:** read and written through the player's live `StatController`
  (`LimitedFloatStat.SetBaseValue`). The `PlayerBaseStats` asset is only the new-game
  template and is never touched.
- **UI:** IMGUI with manual layout using only `GUI.Box/Label/Button/Toggle/TextField`.
  This game's build has `GUILayout` and `GUI.HorizontalSlider` stripped, so they can't be used.

## Releasing

1. Bump the version in `Mod.cs` (`MelonInfo`) and build the release DLL (see below).
2. Zip it as `FLSModMenu-<version>.zip` with the DLL at `Mods/FLSModMenu.dll`.
3. Publish a GitHub release (tag `v<version>`) with the zip attached. The release notes become the Nexus changelog.

Publishing the release runs [`.github/workflows/nexus-upload.yml`](.github/workflows/nexus-upload.yml),
which uploads the zip to [the Nexus page](https://www.nexusmods.com/footballlifesimulator/mods/13)
as a new version of the main file and archives the previous one.

## Build

Requires the .NET SDK (6 or newer) and the game with MelonLoader installed (for reference DLLs).

```
dotnet build -c Release -p:GameDir="C:\path\to\Football Life Simulator"
```

Output: `bin\Release\net6.0\FLSModMenu.dll`. Debug builds add a file-driven test hook
(`UserData\flsmm_test.txt`); release builds don't include it.

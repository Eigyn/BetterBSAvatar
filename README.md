# BetterBSAvatar

BetterBSAvatar is an experimental BSIPA plugin for Beat Saber 1.40.8 that displays and tracks the game's built-in multiplayer avatar outside normal multiplayer contexts.

The plugin clones the built-in avatar visual at runtime, refreshes it from Beat Saber's avatar data, and follows the player head and hands in menus and gameplay.

## Features

- Creates the built-in multiplayer avatar automatically when enabled.
- Tracks the headset and hands in menus and gameplay.
- Can hide the avatar from the first-person headset camera.
- Refreshes avatar visuals after changes in Beat Saber's avatar editor.
- Keeps the clone across scene loads to avoid losing materials or corrupting hand orientation.

## Requirements

- Beat Saber 1.40.8
- BSIPA 4.3.6 or newer compatible 4.x version
- BeatSaberMarkupLanguage 1.12.5 or newer compatible 1.x version
- Visual Studio 2022 Build Tools or another MSBuild setup that can target .NET Framework 4.7.2

## Build

This repository does not include Beat Saber binaries, decompiled files, or game assets. Point `GameDir` at your own local Beat Saber install when building.

```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" .\BetterBSAvatar.csproj /p:Configuration=Release /p:GameDir="C:\Path\To\Beat Saber" /m
```

You can also set `BEAT_SABER_DIR` and omit `/p:GameDir=...`.

```powershell
$env:BEAT_SABER_DIR = "C:\Path\To\Beat Saber"
& "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" .\BetterBSAvatar.csproj /p:Configuration=Release /m
```

The built plugin is written to:

```text
bin\Release\BetterBSAvatar.dll
```

## Install For Local Testing

To copy the DLL into the configured Beat Saber `Plugins` folder:

```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" .\BetterBSAvatar.csproj /p:Configuration=Release /p:GameDir="C:\Path\To\Beat Saber" /p:CopyToGame=true /m
```

Then launch Beat Saber and open `Mod Settings > BetterBSAvatar`.

## Settings

- `Enable Avatar`: creates, displays, tracks, and refreshes the avatar clone.
- `Show in First Person`: shows the avatar in the headset view. When disabled, the avatar is hidden from the first-person camera.

## Repository Hygiene

Only source code, BSML views, metadata, and documentation belong in this repository. Do not commit:

- Beat Saber DLLs or copied dependencies.
- Decompiled IL or game assets.
- `bin/`, `obj/`, PDBs, logs, or local config files.
- Personal install paths or credentials.

## License

No license has been selected yet. Until a license is added, the source is published without an explicit reuse license.

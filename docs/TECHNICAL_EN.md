# Technical documentation

[English version](TECHNICAL_EN.md) | [Русская версия](TECHNICAL_RU.md)

This document describes the architecture of S.T.A.L.K.E.R. Mod Launcher `v1.2.2`, its data storage, launch modes, and safety constraints.

## Purpose and compatibility

The launcher works with local game and modification folders. It does not depend on Steam, cloud services, or a database.

The primary scenario is a DRM-free installation of a S.T.A.L.K.E.R. game plus a set of mod folders that must be overlaid in a defined order. The file model applies to Shadow of Chernobyl, Clear Sky, Call of Pripyat, Anomaly, OGSR, and other builds with a typical X-Ray structure: `gamedata`, `fsgame.ltx`, `bin` or `bin_x64`, and `gamedata.db*` archives.

The launcher is not a replacement for a mod installer. The user selects already prepared game and modification folders.

## Profiles and mod order

A profile stores:

- its name, type, and state;
- the base game path;
- enabled mods and their order;
- the selected EXE, EXE source, arguments, and working directory;
- the launch mode;
- the fixed workspace path;
- profile-specific Discord and Anomaly USVFS settings.

There are two profile types:

- A **regular profile** overlays mods on top of a base game.
- A **standalone profile** starts a ready-to-play build from its own folder without a workspace or layered overlay.

Layers in a regular profile are applied in this order:

```text
base game -> mod 1 -> mod 2 -> ... -> profile writable files
```

A mod lower in the list has higher priority. If two layers contain the same relative path, the game receives the file from the last enabled layer.

Multiple selected mods can be moved as one group. Importing `modlist.txt` transfers the order and enabled state of mods already added to the profile; MO2's highest-first order is converted to the launcher's display order automatically.

## Shared overlay model

Both backends use the same model and do not calculate file order independently.

### FileLayerPlan

`FileLayerPlan` describes the base game, enabled mods, and their priority. It can determine:

- the final source of a relative file;
- every layer that provides a file;
- launch executable candidates;
- files that one mod overwrites in earlier layers.

### OverlayManifest

`OverlayManifest` is a snapshot of important plan results: the selected EXE, system files, writable areas, and their sources. It is used for launch preparation and diagnostics.

### LaunchPlan

`LaunchPlan` is the final launch description. It contains the backend, absolute EXE path, working directory, arguments, and a diagnostic description of the executable source. `ProfileLauncher` starts only a prepared plan.

## Launch modes

### Workspace - stable

`LinkedWorkspace` is the default mode. A regular profile receives a managed structure:

```text
<Workspaces root>\<name at creation>-<short ID>\
  .stalker-launcher-workspace
  current\
  userdata\
  build-manifest.json
```

- `current` is a disposable cache of the resulting game tree;
- `userdata` contains profile saves, settings, logs, screenshots, and writable files;
- `build-manifest.json` contains source signatures and statistics from the last build;
- `.stalker-launcher-workspace` is the safety marker for a launcher-managed directory.

On the same NTFS volume, source files are usually connected with hard links. Symbolic links are used across volumes. Writable files such as `fsgame.ltx` are created locally. The launcher does not silently fall back to copying the entire game: if it cannot create a safe link, launch stops with a clear error.

Creating symbolic links may require **Developer Mode** in Windows or running the launcher as administrator. File Explorer counts the logical size of hard-linked files as separate data even though no additional data blocks are allocated.

The workspace is bound to the profile ID. Renaming a profile does not change the path of an existing workspace.

### USVFS - experimental

`VirtualFileSystem` uses the official [ModOrganizer2/usvfs](https://github.com/ModOrganizer2/usvfs). Game and mod files become visible only to the launched process without physically assembling `current`.

USVFS is not the default mode. If a build is incompatible or unusual, the user can switch back to the stable Workspace mode at any time.

Runtime files placed next to the launcher:

```text
usvfs_x64.dll
usvfs_proxy_x64.exe
usvfs_x86.dll
usvfs_proxy_x86.exe
StalkerModLauncher.UsvfsX86Host.exe
```

x64 games are started through the managed adapter and `usvfs_x64.dll`. x86 games are started by a separate 32-bit helper that loads `usvfs_x86.dll`; the helper remains alive while child game processes are running. Only one USVFS profile may run at a time to isolate the runtime's global state.

When a mod provides the engine, the launcher creates a small `userdata\usvfs-bootstrap`. It physically contains the selected EXE, required loader-time DLLs, and the profile `fsgame.ltx`. When a base-game executable is selected, USVFS may use the physical game root without a bootstrap copy of the engine.

Anomaly Launcher is a 32-bit shell and is started through the separate x86 USVFS host; the engine process it creates inherits the virtual overlay. A manually selected renderer starts the corresponding `AnomalyDX*.exe` directly. A relative executable is resolved through `FileLayerPlan`, so a higher-priority mod executable replaces a base file with the same relative path.

Deeper research notes and proofs of concept are available in [USVFS_RESEARCH_EN.md](https://github.com/ITzSYUK/StalkerModLauncher/blob/main/docs/USVFS_RESEARCH_EN.md).

## Profile data and fsgame.ltx

For a regular profile, `fsgame.ltx` is taken from the final layer and rewritten so that `$app_data_root$` points to the profile `userdata`. Other lines and additional mod aliases are preserved. The source encoding is detected and retained, including Windows-1251.

Typical directories:

```text
userdata\savedgames
userdata\logs
userdata\screenshots
userdata\user.ltx
userdata\writable-game-files
userdata\overwrite
```

`writable-game-files` is used for known configuration files that an engine expects inside the game tree, for example Anomaly `gamedata\configs\localization.ltx`. `overwrite` is intended for new or changed files produced by a USVFS profile. Original game and mod folders remain read-only sources.

On first launch, `user.ltx` is imported from the highest-priority layer that provides it. A profile copy modified by the user or game is preserved; an unchanged lower-layer copy can be safely upgraded when a patch supplies a newer file.

If the game or a mod provides a prepared `shaders_cache`, the final cache files are seeded into `userdata\shaders_cache`. This is required by some Anomaly builds and does not modify the source cache in the game or mod folders.

## File safety

The launcher does not edit or delete original game or mod folders.

The `.stalker-launcher-workspace` marker contains the identifier of a managed workspace. Before cleanup, rebuild, or deletion, the launcher verifies the marker and the allowed Workspaces root. If the marker is missing or belongs to another profile, deletion is blocked. This prevents an arbitrary user folder from being recursively removed by mistake.

Deleting a regular profile removes only its managed workspace. Deleting a standalone profile removes the settings entry but leaves the ready-to-play build folder intact.

## Settings

Settings are stored outside the game:

```text
%APPDATA%\StalkerModLauncher\settings.json
%APPDATA%\StalkerModLauncher\settings.backup.json
```

The current `SchemaVersion` is `4`. During loading, the normalizer fills missing fields, repairs duplicate IDs, and resets the transient `IsRunning` state. Saving uses a temporary file followed by replacement of the main JSON; the backup helps recover from an interrupted write.

Paths are absolute. After moving a game or mod to another drive, its path must be corrected in the profile. A workspace is moved through the built-in command, which updates the path only after the operation succeeds.

The launcher prevents two instances from running simultaneously so they cannot write the same `settings.json` in parallel.

## Validation and diagnostics

Preflight runs before launch and checks:

- the base game and enabled mods;
- the final EXE through the shared layer model;
- the working directory and EXE architecture;
- availability of the required USVFS runtime;
- preparation of profile writable files;
- workspace safety.

The **Profile status** window presents a compact summary: readiness, workspace size and file types, checks, latest log, and crash dump. The application journal records the backend, EXE source, rebuild reasons, link statistics, and launch result.

The journal is rotated and does not grow without limit. Old records are constrained by the logging service settings.

## AP-PRO modification browser

The browser reads public AP-PRO category pages for Shadow of Chernobyl, Clear Sky, and Call of Pripyat. Each card contains a title, short description, cover, rating, view count, and a link to the original page.

Pages load sequentially while scrolling. Category loading is cancelled when the category changes, so an outdated response cannot replace the new list. Search works on received titles and continues to include results from subsequent pages. The "nothing found" message appears only after loading has finished.

The HTTP client identifies itself as `StalkerModLauncher/<version>` and includes the repository URL. A short delay is kept between catalog page requests, and no more than four covers are downloaded concurrently. After `429 Too Many Requests`, the launcher retries once after the server-provided `Retry-After` delay, capped at 30 seconds.

Catalog responses are cached in memory for approximately 10 minutes; covers are decoded lazily. No persistent catalog is saved to disk after the window closes. Internet access is required. The launcher does not bypass site protection and does not download or install mods.

## Build and tests

Development requires the .NET 8 SDK:

```powershell
dotnet build .\StalkerModLauncher.sln -c Release
dotnet test .\StalkerModLauncher.sln -c Release
dotnet run --project .\src\StalkerModLauncher\StalkerModLauncher.csproj
```

Release `v1.2.2` is packaged with:

```powershell
.\scripts\Build-Release.ps1 -Version 1.2.2
```

The script creates two ZIP packages in `publish\release\v1.2.2`:

- framework-dependent: requires .NET 8 Desktop Runtime x64;
- standalone: .NET is included in `StalkerModLauncher-Standalone.exe`.

Both packages contain the required native USVFS files, `LICENSE.txt`, and `THIRD-PARTY-NOTICES.txt`. README and release notes are not duplicated in the user ZIP. Reproducible packaging requires locally built official USVFS x64/x86 artifacts and the x86 helper; these binary artifacts are not stored in Git.

For regular development builds with official USVFS files, use:

```powershell
.\scripts\Build-VfsExperimental.ps1 -CleanPublishRoot
```

## Project structure

```text
src/StalkerModLauncher/
  Models/          JSON models and plans
  ViewModels/      MVVM state and commands
  Views/           WPF windows and components
  Themes/          palette and styles
  Services/        workspace, launch, USVFS, diagnostics, AP-PRO
  Infrastructure/  base MVVM components

native/
  StalkerModLauncher.UsvfsX86Host/

research/
  usvfs-poc/
  usvfs-managed-poc/

tests/StalkerModLauncher.Tests/
```

## Known limitations in v1.2.2

- USVFS remains experimental; unusual wrappers and individual engines may require Workspace mode.
- USVFS requires the Microsoft Visual C++ 2015-2022 Redistributable matching the game architecture.
- Symbolic links across drives depend on Windows configuration.
- Absolute paths do not automatically survive moving a game.
- The modification browser depends on AP-PRO availability and page markup.

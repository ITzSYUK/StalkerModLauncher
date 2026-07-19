# Technical documentation

[English version](TECHNICAL_EN.md) | [Русская версия](TECHNICAL_RU.md) | [Russian user guide](USER_GUIDE_RU.md)

This document describes the current architecture of S.T.A.L.K.E.R. Mod Launcher `v1.2.4`: how profiles are stored, how the winning file is selected, how Workspace differs from USVFS, where profile data is kept, and which checks protect original game and mod folders.

The detailed USVFS research history and experimental prototypes are available in [USVFS_RESEARCH_EN.md](USVFS_RESEARCH_EN.md).

## 1. Scope and compatibility

The launcher works with local game and mod folders. Its main use case is a base game plus an ordered list of folders whose contents must be layered over that game.

Support is based on file structure rather than a hard-coded game title. The file model understands common X-Ray elements:

- `gamedata`;
- `fsgame.ltx`;
- `bin` and `bin_x64`;
- `gamedata.db*` archives, including archives stored in `db` and `patches`;
- `appdata`, `userdata`, and `_appdata_`.

In practice this covers Shadow of Chernobyl, Clear Sky, Call of Pripyat, Anomaly, OGSR, iX-Ray, and many derived projects. Compatibility with every custom launcher or modified engine cannot be guaranteed.

The application is not a mod installer. It does not unpack downloaded archives and cannot repair incompatible game content.

## 2. Core concepts

### Profile

Each profile is stored as a separate launch recipe. It contains:

- profile ID and name;
- profile type;
- base game path;
- the mod list, order, and enabled state;
- selected launch mode;
- relative EXE path and, for a manual choice, its pinned source folder;
- command-line arguments and relative working directory;
- workspace path;
- playtime, last launch time, Discord setting, and Anomaly renderer setting.

Calculated values such as the current running state and formatted dates are not written to JSON.

### Standard profile

A standard profile creates this layer order:

```text
base game -> mod 1 -> mod 2 -> ... -> profile writable data
```

A mod lower in the UI has higher priority. When several enabled layers contain the same relative path, the last layer wins.

### Standalone profile

A standalone profile starts an already assembled game or mod from its own folder. It does not create a layer plan, `current`, or a separate mod overlay. Save and log locations are controlled by that build through `fsgame.ltx` and its usual data directories.

## 3. Launch pipeline

Both launch modes use the same high-level sequence:

1. Game and mod folders, EXE, working directory, and launch-mode availability are validated.
2. A managed workspace is assigned to the ID of a standard profile.
3. One shared layer plan is created and the winning file for every overlapping path is resolved.
4. Workspace or USVFS prepares the final launch parameters.
5. The game starts with the selected EXE, arguments, and working directory.
6. The launcher tracks process exit, playtime, and Discord Rich Presence.
7. After exit, it looks for a new game log and crash dump.

Preparation produces a small instruction set: which mode to use, which absolute EXE to start, which arguments to pass, and which working directory to assign. For USVFS it also keeps a temporary virtual-file-system session alive.

This split keeps the Status window, preflight checks, and actual launch from independently choosing different executables or interpreting mod order differently.

## 4. Shared layer model

### File layer plan

Before validation or launch, the launcher builds one overlay plan for a standard profile. It contains:

- the base game at order `0`;
- enabled mods in user-defined order;
- `userdata` as the final profile-data layer.

The launcher uses this plan to answer four main questions:

- which file at a relative path the game will finally see;
- which layers provide that file;
- which EXE files are available and where they come from;
- which earlier files are overridden by a mod.

Workspace and USVFS consume the same plan instead of implementing separate priority rules.

### Final overlay snapshot

After analysis, the launcher keeps a compact snapshot of the important results:

- layers and their order;
- selected EXE and its source;
- important files such as `fsgame.ltx`, `user.ltx`, and `localization.ltx`;
- writable files;
- the `userdata\overwrite` root;
- overlap information when requested.

This in-memory snapshot is not the same as Workspace `build-manifest.json`. The first describes what should be visible. The second records the latest physical `current` build.

## 5. Executable resolution

Automatic selection examines candidates from the base game and every enabled mod. If the same relative path exists in several layers, the candidate from the highest-priority layer wins.

A manual selection stores two values:

- the EXE path inside the virtual game tree;
- the pinned folder from which that exact EXE must be taken.

This allows a user to intentionally select an executable from a lower layer even when a higher-priority mod contains an EXE with the same name. Returning to automatic selection clears the pinned source.

For Anomaly under USVFS there are two supported paths:

- automatic mode starts `AnomalyLauncher.exe` through a small 32-bit helper, and the game process it creates inherits USVFS;
- a manually selected renderer starts the chosen `AnomalyDX*.exe` directly, including AVX variants.

If a mod provides the selected renderer at the same relative path, the file from the highest-priority layer is selected.

## 6. Workspace: stable mode

The stable mode creates a managed working folder for the profile. By default, its root is placed on the base game's drive:

```text
<game drive>\StalkerModLauncher\Workspaces\<name>-<short ID>\
```

If a drive cannot be resolved, the fallback root is:

```text
%LOCALAPPDATA%\StalkerModLauncher\Workspaces
```

Workspace layout:

```text
<workspace>\
  .stalker-launcher-workspace
  build-manifest.json
  current\
  userdata\
```

### Building current

Before building, the launcher snapshots the base game and mods. It then creates a signature from source paths, file state, mod order, selected EXE, and relevant profile options.

When the signature matches `build-manifest.json` and the selected EXE still exists, `current` is reused. Profile writable files are still restored and validated.

When sources have changed, the workspace is rebuilt:

1. known writable files are collected from the old `current`;
2. the old `current` contents are removed through guarded deletion;
3. the base game is materialized;
4. enabled mods are applied in order;
5. profile `fsgame.ltx`, `user.ltx`, and writable files are prepared;
6. a manually pinned EXE source is enforced when required;
7. a new `build-manifest.json` is written.

### File strategy

The launcher chooses a safe representation for each file:

- hard link when the source and workspace are on the same NTFS volume;
- symbolic link when a cross-volume link is needed;
- local file for configuration that must change independently;
- a dedicated read-only copy only where linking would be unsafe for the source.

There is no silent fallback that copies the whole game. If a safe link cannot be created, preparation stops with an actionable error.

A hard link shares physical data with its source, so writing through it would also change the source. Files that may be modified are therefore never left as ordinary writable hard links. Attributes and local copies protect the original folders.

Explorer counts hard-linked files toward the visible logical size. Actual additional disk usage is calculated from local files and shown separately.

## 7. USVFS: experimental mode

USVFS mode uses the official [ModOrganizer2/usvfs](https://github.com/ModOrganizer2/usvfs) runtime. Instead of creating `current`, it presents one merged virtual tree to the launched game.

The shared layer plan is converted into this mapping order:

```text
base game -> mods in order -> known writable files -> profile overwrite
```

`userdata\overwrite` is mapped last, monitors changes, and has the highest priority.

### x64 and x86

x64 targets are started through the managed adapter and `usvfs_x64.dll`. x86 targets use `StalkerModLauncher.UsvfsX86Host.exe`, which loads `usvfs_x86.dll` in a process with matching architecture.

The x86 host remains alive while injected child processes are active. This is required for launcher applications that exit immediately after starting the actual engine.

Only one USVFS session may run at a time because the official runtime uses shared process state and a shared namespace.

### Virtual-root strategy

The launcher chooses a virtual-root strategy according to the launch layout:

- the physical base-game root is used when its own EXE starts and mods do not provide loader-time files;
- the physical Anomaly root is preferred for reliable access to loose resources;
- a physical X-Ray 1.6 root is used when `$arch_dir_*` entries are present so archives in `patches` and similar directories remain visible;
- an isolated bootstrap root is used when a mod provides the engine or the selected executable needs its own neighboring DLL set.

The physical game directory is never mapped over itself. Doing so can hide real `gamedata.db*` archives from the engine.

### usvfs-bootstrap

`userdata\usvfs-bootstrap` contains only files that Windows and the engine must see physically before full virtual lookup is available:

- selected EXE;
- loader-time DLL files from its directory;
- profile `fsgame.ltx`;
- the smallest required set of neighboring files.

This directory is a service cache, not a full game copy. It is regenerated and is not treated as valuable profile data during a workspace move.

USVFS runtime files distributed beside the launcher are:

```text
usvfs_x64.dll
usvfs_proxy_x64.exe
usvfs_x86.dll
usvfs_proxy_x86.exe
StalkerModLauncher.UsvfsX86Host.exe
```

## 8. Profile data isolation

Persistent data for a standard profile is kept in:

```text
<workspace>\userdata
```

The launcher takes the winning `fsgame.ltx`, preserves its encoding, including Windows-1251, and changes `$app_data_root$` to the profile's absolute `userdata` path. Other aliases and mod-specific lines remain intact.

Common contents include:

```text
userdata\savedgames
userdata\logs
userdata\screenshots
userdata\user.ltx
userdata\shaders_cache
userdata\writable-game-files
userdata\overwrite
userdata\usvfs-bootstrap
```

### user.ltx

On first preparation, the source is selected from the highest-priority layer down to the base game. If the profile copy has already been changed and no longer matches a source file, it is treated as user-owned and preserved.

If the profile copy still equals the previous lower-layer source, a new `user.ltx` from a higher-priority patch may safely replace it.

### Shader cache

Prepared shader caches from layer appdata folders are merged according to priority. Existing user cache files are not overwritten without a reason. This supports Anomaly builds that ship a prepared cache with their fixes or presets.

### Writable files inside the game tree

Some engines write configuration inside the game tree. Known paths such as `gamedata\configs\localization.ltx` receive a separate profile copy under `userdata\writable-game-files`.

Workspace places that copy into `current` and collects changes after use. USVFS maps the same profile file to the expected virtual path.

### Standalone profiles

For a standalone profile, the launcher inspects `fsgame.ltx` and common locations such as `appdata`, `userdata`, `_appdata_`, `bin\_appdata_`, and `bin_x64\_appdata_`. It does not rewrite data routing for that build.

## 9. File safety and workspace lifecycle

Deleting a profile never deletes source game or mod folders.

A managed workspace is protected at two levels:

- `.stalker-launcher-workspace-root` identifies an allowed Workspaces root;
- `.stalker-launcher-workspace` binds a profile folder to its profile ID.

Before recursive cleanup, move, or deletion, the launcher verifies:

- the target is inside an allowed root;
- the marker file exists;
- the short ID matches the profile;
- source and destination do not form an unsafe nested path.

The operation is blocked when validation fails. A missing marker may be restored only when an automatically generated folder unambiguously matches the profile ID.

The workspace path becomes bound to the profile ID after its first assignment. Renaming the profile does not create a new workspace. A copied profile receives a new ID and its own folder.

Deleting a standard profile removes only its validated managed workspace. Deleting a standalone profile removes its settings entry but leaves the standalone build folder untouched.

## 10. Settings and recovery

Settings files are stored at:

```text
%APPDATA%\StalkerModLauncher\settings.json
%APPDATA%\StalkerModLauncher\settings.backup.json
```

The JSON file contains a settings-structure version number. Its current value is `4`. While loading the file, the launcher:

- upgrades the schema version;
- creates missing collections;
- repairs empty or duplicate IDs;
- normalizes mod order;
- resets the temporary running-state marker;
- migrates supported legacy fields.

Saving is atomic as far as the file system permits:

1. a complete snapshot is serialized;
2. it is written to `.tmp`;
3. the main file is replaced;
4. the previous version becomes the backup.

Settings reads and writes are performed one at a time, so two operations cannot edit the file concurrently. A second launcher instance is also blocked.

Game and mod paths are absolute. When a source folder is moved, the user must select it again. The workspace move operation first copies `userdata`, changes the stored path only after success, and then removes the old validated workspace.

## 11. Validation, status, and diagnostics

Preflight validates:

- base game and enabled mod folders;
- final EXE and architecture;
- working directory and arguments;
- availability of USVFS runtime files for the target architecture;
- readiness of `fsgame.ltx` and profile data;
- workspace safety markers;
- common loader-time DLL files near the selected engine.

Errors block launch. Warnings describe unusual layouts that may still be valid.

The Status window uses the same models and displays a compact summary. Workspace statistics are read from `build-manifest.json` without rescanning the full tree. In USVFS mode it shows layers and profile-data readiness because `current` does not exist.

Application logs are stored at:

```text
%APPDATA%\StalkerModLauncher\launcher.log
%APPDATA%\StalkerModLauncher\launcher.old.log
```

The current log rotates at 1 MB, replacing `launcher.old.log` with the previous log.

Game logs and dumps are searched in profile `userdata` or the common data folders of a standalone build. Diagnostics use the launch timestamp so an old crash dump is not reported as the result of the current session.

## 12. Mod management

Scanning searches recursively for mod roots, but stops treating nested folders as separate mods after a valid root is found. It recognizes unpacked files and X-Ray archives, including `db` and `patches` directories.

Grouped movement preserves the relative order of selected mods. The UI supports drag and drop plus move-to-start and move-to-end commands.

Import from `modlist.txt` matches entries to mods already added to the profile and imports enabled state and order. MO2 order is converted to the launcher's rule that lower entries have higher priority.

Overlap analysis treats file replacement as priority information, not as an error. An overlap does not prevent a mod from being disabled.

## 13. AP-PRO modification browser

The browser reads the public Shadow of Chernobyl, Clear Sky, and Call of Pripyat categories. It does not download or install modifications.

Network behavior is deliberately conservative:

- honest `User-Agent: StalkerModLauncher/<version>` with a repository link;
- sequential page loading with a short delay;
- no more than four simultaneous cover-image downloads;
- one retry after `429 Too Many Requests`, honoring `Retry-After` up to 30 seconds;
- cancellation of an old category load when the user switches categories;
- an in-memory cache lasting about 10 minutes;
- lazy image decoding.

Search filters loaded titles and continues to include later pages. The empty-result message appears only after the category load has finished.

The browser remains dependent on AP-PRO availability and HTML structure.

## 14. Additional features

- Screenshot discovery finds PNG, JPG, and BMP files in profile and standalone data locations.
- Clipboard copying releases the full-size image after transfer instead of keeping it in memory unnecessarily.
- Discord Rich Presence publishes profile and launch state only when the user enables the option.
- Update checking is manual and compares the installed version with the latest GitHub release tag.
- UI sounds reuse embedded OGG decoders instead of creating a new decoder on every click.
- Child-window navigation is kept outside the main window so its code can focus on display and user actions.

## 15. Project structure

```text
src/StalkerModLauncher/
  Models/          persisted data and final launch parameters
  ViewModels/      WPF state and commands
  Views/           windows and reusable controls
  Themes/          palette and styles
  Services/        launch, workspace, USVFS, diagnostics, AP-PRO
  Infrastructure/  MVVM base types and commands

native/
  StalkerModLauncher.UsvfsX86Host/

research/
  usvfs-poc/
  usvfs-managed-poc/

tests/StalkerModLauncher.Tests/
```

At application startup, one shared module creates the required components. Workspace is always available. USVFS is enabled only when its runtime files are present or the research feature flag is active.

The UI follows MVVM without an external dependency-injection container. Main-screen logic is divided by user scenario, and major areas of the window are separate controls.

The classic and PDA interfaces share the same data model and commands. In PDA mode, settings, profile status, the catalog, log, screenshots, and the profile creation wizard are hosted inside one shell; only the true fullscreen screenshot viewer opens a separate monitor-sized window.

## 16. Build, tests, and release packaging

.NET 8 SDK is required for development:

```powershell
dotnet build .\StalkerModLauncher.sln -c Release
dotnet test .\StalkerModLauncher.sln -c Release
dotnet run --project .\src\StalkerModLauncher\StalkerModLauncher.csproj
```

Complete release packaging:

```powershell
.\scripts\Build-Release.ps1
```

The script reads the version from the project file and verifies formatting, the Release build, and all unit tests before packaging.

The script creates two ZIP archives:

- a compact framework-dependent package that requires .NET 8 Desktop Runtime x64;
- a self-contained package with the .NET runtime included.

Both packages contain the official x64/x86 USVFS runtime, x86 host, `LICENSE.txt`, and `THIRD-PARTY-NOTICES.txt`. PDB, JSON, Markdown, and intermediate files are excluded from user ZIP files.

Experimental VFS publish:

```powershell
.\scripts\Build-VfsExperimental.ps1 -CleanPublishRoot
```

Official USVFS native artifacts and the x86 host must be prepared locally. Compiled third-party binaries are not stored in Git.

## 17. Known limitations

- USVFS remains experimental. Workspace is available for builds that are not compatible with it.
- USVFS requires the Microsoft Visual C++ 2015-2022 Redistributable matching the target game's architecture.
- Cross-drive symbolic links depend on Windows configuration.
- Absolute game and mod paths are not repaired automatically after folders are moved.
- A standalone profile cannot guarantee separate saves if the build itself writes to a shared external folder.
- Automatic EXE and mod-root detection cannot replace the instructions supplied by a specific mod author.
- The modification browser depends on AP-PRO availability and HTML structure.

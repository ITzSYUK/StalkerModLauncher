# USVFS research branch

This branch is intentionally separate from `main`. The stable launcher mode is still
`LinkedWorkspace`; this branch is for researching integration with
[ModOrganizer2/usvfs](https://github.com/ModOrganizer2/usvfs) without risking the
current workspace-based launch path.

## What was verified

`ModOrganizer2/usvfs` is a native C++ library that creates process-local virtual
file links by using API hooking. Its public README describes the goal as links
visible only to selected processes, including overlaying multiple directories
over one destination. The same README says the project is GPLv3 licensed and
is built with CMake, Python 3+ and vcpkg:

```powershell
cmake --preset vs2022-windows-x64
cmake --build --preset vs2022-windows-x64 --config Release
```

The public header exposes the important controller/runtime functions:

- `usvfsCreateVFS` / `usvfsConnectVFS`
- `usvfsVirtualLinkFile`
- `usvfsVirtualLinkDirectoryStatic`
- `usvfsCreateProcessHooked`
- `usvfsCreateVFSDump`

That shape fits our launcher better than the removed custom hook experiment:
our managed code should prepare a mapping plan, while the native library owns
process injection and filesystem interception.

## Current safe bridge

The branch adds `UsvfsMappingPlanBuilder`. It does not load USVFS, does not
inject into games, and does not change runtime behavior.

It converts the existing launcher model into operations that an external USVFS
adapter can later apply:

```text
base game root -> virtual game root
mod 1 root     -> virtual game root
mod 2 root     -> virtual game root
...
known writable files -> exact virtual file paths
userdata\overwrite -> virtual game root, create target
```

This keeps one source of truth:

- `FileLayerPlan` defines layer order.
- `OverlayManifest` defines executable, writable files and overwrite storage.
- `UsvfsMappingPlanBuilder` translates those layers into future USVFS operations.

## Why this direction

The removed custom VFS tried to solve too many native-hooking problems inside
this launcher. It crashed too often and did not reliably apply mod layers.

The new direction is narrower:

1. Keep `LinkedWorkspace` as the stable launch backend.
2. Keep `FileLayerPlan` and `OverlayManifest` as shared logic.
3. Build a small adapter around the official USVFS API.
4. Enable it only behind an explicit experimental setting after a real native
   proof of concept works.

## Next steps

1. Build `ModOrganizer2/usvfs` x64 locally with Visual Studio Build Tools,
   CMake and vcpkg.
2. Create a tiny native/managed proof of concept outside game launch:
   - create a VFS instance;
   - map two test folders over one virtual root;
   - launch a simple process through `usvfsCreateProcessHooked`;
   - verify the process sees the overlaid files.
3. Only after that, add an adapter project or runtime wrapper to the launcher.
4. Keep the UI disabled until the adapter can pass a simple integration test.

## Important constraints

- Do not replace `LinkedWorkspace` while USVFS is experimental.
- Do not reintroduce custom hook DLLs.
- Do not let USVFS write into game or mod folders.
- All new/changed files must still target profile-controlled writable storage.

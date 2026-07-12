# S.T.A.L.K.E.R. Mod Launcher v1.2.1

`v1.2.1` is a maintenance release focused on reliability and presentation. It keeps both launch modes introduced in `v1.2.0`: linked Workspace and Mod Organizer 2 USVFS for x64 and x86 games.

## Changes

- Fixed the scanned-mod selection window failing to open because its owner had not yet been shown.
- Fixed playtime tracking for x86 games launched through the USVFS helper.
- Simplified the **Profile status** window:
  - removed obsolete virtual-file diagnostics;
  - added a compact USVFS summary for layers, profile data, and `current` usage;
  - moved the USVFS explanation into a tooltip.
- Refined the release packaging scripts and bilingual technical documentation.
- Updated the project logo and README presentation.

## Downloads

- `StalkerModLauncher-v1.2.1-win-x64.zip` is the compact build. It requires the [.NET 8 Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/8.0).
- `StalkerModLauncher-v1.2.1-win-x64-standalone.zip` includes the .NET runtime and does not require a separate .NET installation.

Both packages include the official USVFS runtime files required by the launcher. USVFS additionally requires the Microsoft Visual C++ 2015-2022 Redistributable matching the game architecture.

## Notes

Workspace remains the most compatible mode. USVFS avoids building a full `current` tree, but compatibility can depend on the particular X-Ray engine or wrapper. If a profile behaves incorrectly with USVFS, switch that profile to **Workspace - stable**.

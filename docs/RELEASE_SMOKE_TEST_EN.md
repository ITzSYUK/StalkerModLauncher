# Manual pre-release smoke test

Automated tests cover internal logic and XAML compilation, but they cannot reliably reproduce WPF window behavior or real game launches. Complete this short smoke test before publishing a release.

## Interface

- Start the launcher and confirm that the main window opens without errors.
- Open About, switch to the PDA interface, and return to the classic interface.
- In the PDA interface, open the profile, catalog, log, About, profile list, health, screenshots, and settings pages.
- Close the launcher from both interfaces and confirm that no second window remains in the background.

## Profiles

- Create a test standard profile, save its settings, and restart the launcher.
- Test copying, exporting, and deleting the test profile.
- Reorder two mods and confirm that their order survives a restart.

## Game launch

- Launch one profile with Workspace and check saves, the log, and playtime tracking.
- Launch one compatible profile with USVFS; test x64 and x86 separately when suitable games are available.
- After exit, open Health and confirm that the latest log and crash dump are detected correctly.

Run `scripts\Build-Release.ps1` only after this checklist passes.

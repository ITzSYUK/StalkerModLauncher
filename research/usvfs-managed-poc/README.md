# Managed USVFS PoC

This research-only console app verifies the managed adapter in
`StalkerModLauncher.Services.UsvfsRuntime`.

It uses the official `usvfs_x64.dll` through P/Invoke, applies a
`UsvfsMappingPlan`, starts itself as a hooked child process and checks that the
child sees the overlaid files.

Example:

```powershell
dotnet run --project .\research\usvfs-managed-poc\StalkerUsvfsManagedPoc.csproj -- "$env:TEMP\stalker-usvfs-src\usvfs"
```

This project is not part of the normal launcher runtime. It remains an isolated
smoke-test harness for the stable production USVFS backend.

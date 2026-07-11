# USVFS Overlay PoC

Small external proof of concept for `ModOrganizer2/usvfs`.

It creates two source folders and maps them into one virtual root:

```text
base -> virtual-root
mod  -> virtual-root
```

The child process is launched through `usvfsCreateProcessHooked`. It must see:

- files that exist only in `base`;
- files that exist only in `mod`;
- the `mod` version of files that exist in both layers.

This folder is research-only and is not used by the launcher at runtime.

# Incident index — search by symptom

**Look up what you saw, not what it turned out to be.**

| What you see | What it actually was | Record |
|---|---|---|
| Deleted a Unity package's leftover asset files (`Assets/XR/`, `Assets/XRI/`, a settings-asset reference), they come back after the next refresh, even though `Packages/manifest.json` stays clean | The package's assemblies were still loaded in the live Editor process — no domain reload had happened, so its `[InitializeOnLoad]` code kept regenerating them | [2026-08-20-removed-package-but-settings-files-keep-coming-back.md](2026-08-20-removed-package-but-settings-files-keep-coming-back.md) |
| Unity Editor menu command that shells out to a CLI tool does nothing / fails, even though the tool is confirmed installed and on PATH from a fresh terminal | The Editor process (or subprocess it spawned) was launched before the tool was installed and never picked up the updated PATH | [2026-08-20-unity-menu-command-cant-find-a-tool-installed-moments-earlier.md](2026-08-20-unity-menu-command-cant-find-a-tool-installed-moments-earlier.md) |
| A child object's size in the world is wildly different from the size the code sets on it, with correct config values and no errors anywhere | The child was parented under a root that was itself scaled, so every local scale got multiplied on the way to world space | [2026-08-20-ring-radius-wildly-off-from-sphere-radius.md](2026-08-20-ring-radius-wildly-off-from-sphere-radius.md) |

## Writing an entry

File name `YYYY-MM-DD-<symptom-phrase>.md`. Each entry needs: **Symptom** (including what
was absent), **Why it was hard**, **What was true** (with the measurement), **Two-minute
path next time**. Only write up faults that were expensive because the evidence misled.

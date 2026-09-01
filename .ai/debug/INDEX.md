# Incident index — search by symptom

**Look up what you saw, not what it turned out to be.**

| What you see | What it actually was | Record |
|---|---|---|
| Deleted a Unity package's leftover asset files (`Assets/XR/`, `Assets/XRI/`, a settings-asset reference), they come back after the next refresh, even though `Packages/manifest.json` stays clean | The package's assemblies were still loaded in the live Editor process — no domain reload had happened, so its `[InitializeOnLoad]` code kept regenerating them | [2026-08-20-removed-package-but-settings-files-keep-coming-back.md](2026-08-20-removed-package-but-settings-files-keep-coming-back.md) |
| Unity Editor menu command that shells out to a CLI tool does nothing / fails, even though the tool is confirmed installed and on PATH from a fresh terminal | The Editor process (or subprocess it spawned) was launched before the tool was installed and never picked up the updated PATH | [2026-08-20-unity-menu-command-cant-find-a-tool-installed-moments-earlier.md](2026-08-20-unity-menu-command-cant-find-a-tool-installed-moments-earlier.md) |
| A child object's size in the world is wildly different from the size the code sets on it, with correct config values and no errors anywhere | The child was parented under a root that was itself scaled, so every local scale got multiplied on the way to world space | [2026-08-20-ring-radius-wildly-off-from-sphere-radius.md](2026-08-20-ring-radius-wildly-off-from-sphere-radius.md) |
| A video shows only its first frame - no motion, no sound - while `isPrepared` and `isPlaying` both report true and nothing errors | Unity decodes H.264 through the OS, which takes 18-56s to deliver a first picture on this machine. **Fix: set the importer to transcode to VP8** (1.76s). Originally compounded by code that reset the decoder every loop | [2026-08-28-video-shows-first-frame-only-and-never-advances.md](2026-08-28-video-shows-first-frame-only-and-never-advances.md) |
| A video shows only its first frame while the take plays normally around it, `frame` reads **0** (not -1), `paused` is true, and there are no errors *or warnings* | Nobody resumed it. `DancePlayer.Update` still read the serialized-only `screen` field after each stage got its own VideoPlayer, so the resume condition could never be true. **`frame=-1` is a decoder problem, `frame=0` is a caller problem** | [2026-09-01-video-frozen-on-frame-one-again-after-per-stage-players.md](2026-09-01-video-frozen-on-frame-one-again-after-per-stage-players.md) |
| VR controllers look missing / frozen at the rig origin and don't follow your hands, but the thumbstick still moves you and nothing errors | XR input profile (OpenXR interaction profile) misconfigured — bindings resolve per-control, so pose was starved while buttons and sticks kept working | [2026-08-20-controller-models-dont-track-but-thumbstick-works.md](2026-08-20-controller-models-dont-track-but-thumbstick-works.md) |

## Writing an entry

File name `YYYY-MM-DD-<symptom-phrase>.md`. Each entry needs: **Symptom** (including what
was absent), **Why it was hard**, **What was true** (with the measurement), **Two-minute
path next time**. Only write up faults that were expensive because the evidence misled.

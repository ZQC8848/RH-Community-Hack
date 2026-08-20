# The interaction system is built as a self-contained, prefab-packaged module, not wired directly into this project's scene

**2026-08-20 ・ status: standing ・ scope: covers the ring/sphere spawning, beat-timing, and hit-detection interaction loop only — not networking, not level/beatmap authoring tooling**

## What forced the choice

The project is single-player for now (see [defer-multiplayer.md](defer-multiplayer.md)),
but the interaction system is expected to move into a different, multiplayer-capable
project later. Built the default way — scripts and objects wired directly into this
project's specific scene, camera rig, and input setup — moving it later means re-deriving
which parts are "the interaction system" versus "this project's scaffolding": effectively
a rewrite.

## Decision

Build the ring/sphere/beat-timing/hit-detection loop as one self-contained prefab (or a
small, clearly-bounded set of prefabs) with a narrow public API — spawn/configure/
event-callback surface — and no hard dependency on this project's specific camera rig,
input bindings, or scene layout beyond a documented minimum (e.g. "needs a `Transform`
representing the tracked controller"). No interaction code exists yet as of this
decision — it governs how the code gets written, not a refactor of something already
built.

## Why not the alternatives

| Alternative | Its case | Why not |
|---|---|---|
| Wire it directly into this project's scene/hierarchy, extract into a prefab later if needed | Faster to iterate with fewer abstraction layers up front | "Extract later" on a rhythm-game interaction loop usually means untangling references to this project's specific XR rig/camera/input setup after the fact — the exact rewrite this decision exists to avoid |
| Design a full plugin/UPM package now (own asmdef, own package manifest) | Cleanest possible portability | Overkill for a hackathon-phase project with no second consumer yet; a well-bounded prefab + scripts proves portability without the overhead of maintaining a package |

## What this rests on

| Assumption | Status | Evidence, or the check that would settle it |
|---|---|---|
| Hit detection ("controller reaches into a sphere") doesn't need anything multiplayer-sync-specific baked in — doesn't need to know `NetSyncManager` exists | unverified | Check when STYLY NetSync is reintroduced: does remote-avatar hit detection need new code, or does it reuse the same prefab's public API unchanged? |
| A single `Transform` reference (the tracked controller) is sufficient input for the prefab — no dependency on STYLY XR Rig specifically | unverified | Settle once the first version exists: try driving it from a plain camera+controller `Transform` with STYLY XR Rig absent from the scene |

## Accepted costs

Slightly more upfront design work — defining the prefab's public API/event surface —
before the first playable version exists, versus wiring spawn logic straight into the
scene.

## What would reverse this

- The interaction loop turns out to need something from this specific project's scene
  that can't reasonably be abstracted (e.g. a hard dependency on STYLY XR Rig internals)
  — if hit, downgrade this from "prefab, zero scene dependency" to "prefab, documented
  dependency on STYLY XR Rig"
- Multiplayer work resumes and the prefab, as built, can't be dropped into the new
  project without changes — record why (a debug entry or an update here) and adjust the
  API surface

---

## When this is superseded

*(not yet superseded)*

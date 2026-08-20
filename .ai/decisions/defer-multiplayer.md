# Multiplayer sync is deferred; project scope is single-player only for now

**2026-08-20 ・ status: standing ・ scope: the project-phase scope call, not the interaction system's internal architecture (see [modular-portable-interaction.md](modular-portable-interaction.md) for that)**

## What forced the choice

STYLY NetSync + Meta OpenXR SDK were fully installed and wired into the scene
(`NetSyncManager`, STYLY XR Rig, a verified end-to-end handshake against a local server)
before the project's actual near-term scope was clarified: this phase only needs the
single-player rhythm-interaction loop to work. Carrying the networking stack — extra
packages, an XR SDK dependency, a `NetSyncManager` GameObject, a running external server
process — adds surface area to a phase of the project that doesn't use any of it yet.

## Decision

Revert the NetSync/Meta OpenXR integration (git commit `b54c141`) and build/test the
interaction system without any networking dependency for now.

## Why not the alternatives

| Alternative | Its case | Why not |
|---|---|---|
| Keep NetSync integrated, build single-player features on top of it | Avoids redoing the setup later; it was already working | Adds an XR SDK dependency and a running external server as a precondition for testing anything, for a phase that uses neither |
| Build single-player with no portability requirement, worry about multiplayer later | Fastest path to a playable interaction loop | Risks the interaction system ending up hard-wired into this project's specific scene/GameObjects, forcing a rewrite instead of a drop-in later — see [modular-portable-interaction.md](modular-portable-interaction.md) |

## What this rests on

| Assumption | Status | Evidence, or the check that would settle it |
|---|---|---|
| STYLY NetSync is still the intended multiplayer path when this returns | unverified | Re-check when multiplayer work resumes. The full install procedure is repeatable: `openupm add -f com.styly.styly-netsync@0.17.4`, accept the OpenUPM scoped-registry prompt, run `STYLY/Setup HMD SDKs/Setup Meta OpenXR SDK` (or whichever HMD target applies), add `NetSyncManager` + STYLY XR Rig to the scene |
| The interaction system can be made portable without the networking layer present to test against | unverified | Only known once the interaction prefab exists and someone tries dropping it into a second project |

## Accepted costs

Redoing the STYLY NetSync + Meta OpenXR SDK setup from scratch when multiplayer work
resumes. Not a large cost — done once already, start to finish including verification,
in under 30 minutes.

## What would reverse this

- Multiplayer/LBE sync becomes an active requirement again for this hackathon phase
- The single-player interaction prefab (see [modular-portable-interaction.md](modular-portable-interaction.md)) is built and validated, and the next step is proving it inside a multiplayer-capable project

---

## When this is superseded

*(not yet superseded)*

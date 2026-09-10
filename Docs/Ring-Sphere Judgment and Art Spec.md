---
status: v1 (rules 3.1 / 3.2 confirmed, 2026-08-20)
date: 2026-08-20
related: "Idea - VR Rhythm Game Interaction Paradigm.md" (same folder, high-level concept)
---

# Ring-Sphere Judgment and Art Spec

This document expands section 2.1 of the design doc, "a single beat", and focuses on **the hit
judgment logic and visual presentation of one beat**. It is an implementation spec for
programmers and designers; it does not cover "how a chart is generated from a recorded
performance" (that is sections 2.2 / 3 of the design doc).

## 1. Judgment state machine

```
Spawned → Approaching → touch happens → Resolved(Perfect / Good / Miss-Touch)
                      ↘ expires untouched → Expired → Miss-Timeout
```

- **Spawned**: sphere + ring appear at time `T_spawn` at the given position. The ring's initial
  radius is `R_ring_start`; the sphere's radius is fixed at `R_sphere`.
- **Approaching**: the ring's radius shrinks from `R_ring_start` to `R_sphere` over time, along
  `Ring Shrink Curve`. In principle ring radius == `R_sphere` exactly at `T_perfect`.
- The instant the player's controller enters the sphere's judgment volume (a collider, which may
  be slightly larger than the visual sphere - see the variable table) is recorded as the touch
  time `t`:
  - `t` inside `[T_perfect - PerfectWindow, T_perfect + PerfectWindow]` → **Perfect**: wide
    particle burst + strong haptics + Perfect sound, then Resolved.
  - `t` inside the Good Window but outside the Perfect Window (early and late windows may be
    asymmetric) → **Good**: small particle burst + medium haptics + Good sound, then Resolved.
  - `t` outside the Good Window while the judgment volume is still active (far too early or far
    too late, but not yet at `Expire`) → **Miss-Touch** (below), then Resolved.
  - `t` later than `Expire` (judgment volume already disabled) → **no response**. The judgment
    volume MUST be disabled on the same frame the Miss-Timeout vanish animation starts, so that
    nothing can be triggered accidentally while that animation plays.
- **Expired**: still not Resolved by `T_perfect + GoodWindow(late)` with no touch at all → the
  judgment volume is disabled immediately and, on the same frame, the **Miss-Timeout** shrink-away
  animation begins (its own `Vanish Duration`), with weak feedback (no or very quiet miss sound,
  no haptics).

The two ways of not hitting well are deliberately given opposite animations, so a player can tell
"I touched it badly" from "I never touched it at all" by feel:

- **Miss-Timeout** (never touched) → **shrinks** away (echoing the ring's shrinking language).
- **Miss-Touch** (touched, but outside the Good Window) → **grows and fades out** (distinct from
  the "burst" of Perfect/Good - this is expanding and dissipating, not exploding).

## 2. Judgment grades and feedback

| Grade | Condition | Visual | Sound | Haptics |
|---|---|---|---|---|
| Perfect | `\|t - T_perfect\| ≤ PerfectWindow` | wide burst of particles | Perfect sound | strong |
| Good | `PerfectWindow < \|t - T_perfect\| ≤ GoodWindow` | small burst of particles | Good sound | medium |
| Miss-Touch (touched but off) | touched, but `\|t - T_perfect\|` exceeds GoodWindow (early or late) | sphere grows and fades to nothing (no burst) | TBD; recommended a negative sound weaker than Perfect/Good but clearly more than silence (a sinking / deflating sound) | TBD; recommended light and short, clearly distinct from Perfect/Good's strong/medium |
| Miss-Timeout (never touched, expired) | no touch at all by Expire | sphere + ring shrink to nothing (no burst) | weak / none | none |

`Miss-Touch`'s sound and haptic strength are not yet decided. Use defaults that follow the
principle "more noticeable than Miss-Timeout, weaker than Good"; the actual numbers are the
designer's to tune in the variable table (see the added rows in section 6).

## 3. Rule boundaries (confirmed 2026-08-20)

### 3.1 What is an early/late touch that falls outside the Good Window?
**Confirmed: not "no response", but a third judgment of its own, `Miss-Touch`** - the sphere grows
and fades until gone, visually distinct from Perfect/Good's burst and from Miss-Timeout's shrink.
(The earlier draft's recommended "option A: no response" was not adopted.)

### 3.2 Can the controller still touch the sphere after the window closes, while the Expired vanish animation plays?
**Confirmed: no.** Disabling the judgment volume and starting the Miss-Timeout vanish animation
must happen on the same frame.

## 4. Ring rendering (decided)

The ring is a **billboard** (a flat disc/annulus always facing the player), not a real 3D torus.
The sphere stays a real 3D mesh (Fresnel rim glow holds up from any angle). Reasoning in
[../.ai/decisions/ring-art-direction.md](../.ai/decisions/ring-art-direction.md).

## 5. Art direction (decided: option A - neon energy pulse)

- Sphere: Fresnel rim glow + emission.
- Ring: radially graded glowing annulus + additive blending.
- Judgment grade distinguished by glow colour: Perfect = gold/white, Good = blue, timed-out miss =
  grey dissipation.

Option B (cartoon candy bubble) and option C (particle-native ring) were discussed but not
adopted; reasoning in [../.ai/decisions/ring-art-direction.md](../.ai/decisions/ring-art-direction.md).

## 6. Designer-tunable variables

These should be a ScriptableObject config (one per difficulty / musical style) rather than being
hard-coded on the prefab.

| Category | Variable | Notes |
|---|---|---|
| Time | Ring lead time | How long from spawn to the "perfect moment"; banded by BPM / difficulty |
| Time | Perfect window (±ms) | |
| Time | Good window (early/late may be asymmetric) | |
| Time | Timeout duration | How long after spawn without a hit before it starts shrinking away |
| Time | Miss-Timeout vanish duration + curve | Independent of the timeout judgment itself (shrink away) |
| Time | Miss-Touch vanish duration + curve | Independently controlled (grow + fade); may differ from Miss-Timeout |
| Visual | Ring start radius / sphere radius | Directly affects difficulty and how reachable it feels |
| Visual | Ring shrink curve (AnimationCurve) | Linear vs. eased |
| Visual | Miss-Touch growth factor | How many times the visual radius the sphere expands to before fully transparent |
| Visual | Particle prefab per judgment grade | Not hard-coded, so skins can be swapped (Miss-Touch/Miss-Timeout have no particles; only Perfect/Good need them) |
| Visual | Judgment volume vs. visual sphere size | The judgment volume may be slightly larger than the visual sphere, as tolerance |
| Feedback | Sound per judgment grade (including Miss-Touch, values TBD) | |
| Feedback | Controller haptic strength/duration per grade (including Miss-Touch, values TBD) | |
| Rule | Which controllers may trigger (left / right / either) | Leaves an interface for two-handed and parallel-sphere charts |

## 7. Explicitly out of scope for this spec (needs its own design)

- How a score / combo system consumes judgment grades
- Conflict rules for two-handed or parallel-sphere charts
- The algorithm that extracts beats automatically from a recorded performance (the biggest open
  problem in the design doc)

## 8. Development test mode (non-VR, from 2026-08-20)

The first stage is tested without VR or a headset, simulating input from the keyboard, to verify
only **whether the judgment windows and feedback behave as expected**. It does not test the
spatial dimension of reaching for a sphere.

- **E key**: spawn a test sphere (with its ring) at a fixed position in front of the camera, using
  a default set of designer variables.
- **Space key**: simulate a controller touch - the moment of the keypress is the touch time `t`,
  and the normal judgment path runs (Perfect / Good / Miss-Touch) against the currently live
  (not yet Resolved/Expired) test sphere. If several unresolved spheres exist, the earliest-spawned
  one is hit first.

**Architectural requirement (echoing
[.ai/decisions/modular-portable-interaction.md](../.ai/decisions/modular-portable-interaction.md))**:
the keyboard simulation must call **exactly the same** public trigger interface a real VR
controller collider does (e.g. `BeatTarget.TryTouch(t)`), through a separate "test input adapter"
script. The core judgment logic may not branch on keyboard input. That adapter script is not part
of the "portable to another project" core prefab; it is an editor/test-only add-on, and hooking up
a real VR controller later only means swapping the input source (collider trigger instead of
keypress) without touching the judgment logic.

**Accepted limitation**: this mode does not test spatial judgment (how large the ring reads, how
far the reach feels), only the timeline judgment and the rhythm of the feedback. Validating the
spatial feel has to wait until a real VR controller / XR Rig is connected.

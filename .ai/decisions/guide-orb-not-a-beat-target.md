# The guide orb reuses BeatTarget's art, not its logic

**2026-08-29 ・ status: standing ・ scope: the follow-along guide orbs that travel a recorded take - not the beat judgment loop, not the capture system, not how takes become beatmaps**

## What forced the choice

A new interaction: two orbs travel along recorded controller motion, trailing particles, and
light up when the player's controller reaches into them. Spec:
[../../Docs/Guide Orb 跟随引导球规格.md](../../Docs/Guide%20Orb%20跟随引导球规格.md).

The obvious move was to reuse `BeatTarget`. It already has a sphere, a neon shader, hit
particles, hand-specific rules, and a working contact path - on the face of it this is the
same object with the timing turned off.

It is not. `BeatTarget`'s entire structure is organised around **one moment**: ring lead
time, perfect/good windows, resolve, expire, destroy. Every field answers "how far are we
from that moment". The guide orb has **no such moment** - it exists continuously for the
length of a take, and the player's state is not "hit / missed" but a continuous "how deep is
your hand". Reusing the type would mean a `BeatTargetConfig` where every timing field holds
a junk value that exists only to be ignored, and a state machine whose states are all
unreachable.

## Decision

**Draw the reuse line at the art layer.** Share the neon energy-pulse *direction* and the
"right hand is the cooler hue" relationship. Write a new, much smaller `GuideOrb` state
machine and a separate `GuideOrb.shader`.

Amended 2026-08-29: the shared *hues* were withdrawn. Guide orbs use green (right) / amber
(left) against the beats' cyan / magenta, and their own particle material copied from the beat
burst rather than the same asset - see the mistaken-identity row below. What is still shared is
the visual language, not the palette.

Layering, which follows [modular-portable-interaction.md](modular-portable-interaction.md):

```
DancePlayer         (existing, DanceCapture/)  clock, reference frame, sampling -> proxy pose
  └─ GuideOrb       (new, Interaction/)        knows only "how close is a hand"
       └─ HandTrail (new, Interaction/)        draws a fading line behind a moving point
DanceFollowScore    (new, DanceCapture/)       owns take length, pass boundaries, follow rate
```

`GuideOrb` **does not know that recorded data exists**. It needs its own transform and two
hand transforms, nothing else, so it can ride anything that moves. All knowledge that binds
to the capture system lives in `DanceFollowScore`.

`HandTrail` (added 2026-08-29) knows even less: a driver hands it a position and a gate flag
each frame, and it owns the buffering, ageing and rendering. Splitting it out kept `GuideOrb`
from growing a second job alongside judging contact.

One change to existing code: `DancePlayer` gains an `OnPassStarted` event, because loops go
through `StartPass` while `Play()` runs only once, leaving no signal on which to reset a
per-pass statistic.

## Why not the alternatives

| Alternative | Its case | Why not |
|---|---|---|
| Add a "guide mode" flag to `BeatTarget` | No new type; one place for all orb behaviour | Every timing field, every judgment window and all four `JudgmentResult` values become dead weight guarded by `if (!guideMode)`. The judgment loop is the game's core - loading it with a second unrelated mode makes the thing most likely to need careful edits the thing hardest to edit safely |
| Subclass `BeatTarget` | Inherits the art wiring for free | Inherits the state machine too, and would have to neuter it. Liskov aside, the base class's `resolved` / `pendingVanishResult` have no meaning here |
| Put follow-rate tracking inside `GuideOrb` | Fewer components; the orb already knows when a hand is in it | Take length and pass boundaries are capture-system knowledge. Putting them in the orb permanently welds a portable interaction component to this project's recording format |
| Reuse `HandTouchSource`'s trigger collider | Already written, already works | `OnTriggerEnter` is an **entry event**; this needs continuous inside/outside state, which a per-frame distance comparison gives directly and without a Rigidbody/isTrigger setup. (The original rationale also cited depth as a free continuous value; that stopped applying on 2026-08-29 when entry became a fixed threshold rather than a gradient.) |
| One shared sphere shader for both | One shader to maintain | Two sets of properties on one shader means each side's dials constrain the other's. They are different objects now |

## What this rests on

| Assumption | Status | Evidence, or the check that would settle it |
|---|---|---|
| **Players will not mistake a guide orb for a hittable beat** | **unverified - the biggest risk here**, partly mitigated | Originally both types shared the cyan/magenta hues. Guide orbs were moved to their own palette (green = right, amber = left) on 2026-08-29 specifically to separate them, keeping only the "right is the cooler hue" relationship so the hand mapping still transfers. They remain glowing neon spheres of the same size and style, so this is reduced, not settled. Check on the first headset session with someone who has played the beat scene |
| Per-frame distance testing catches fast hand movement | unverified | A hand crossing 20cm in one frame can pass clean through the 0.2025m detection sphere without any frame sampling inside it. Continuous state does not save us - no frame is ever inside. If this shows up, swap to a swept capsule test between last and current hand position |
| The two orb types stay visually related as art evolves | unverified | If the beat orbs get an art pass, someone has to decide whether guide orbs follow. Two shaders means that is a decision, not an automatic consequence |

## Accepted costs

Two similar sphere shaders and two prefab families to maintain; an art change to the neon
look has to be applied in two places. Accepted because the alternative couples the guide
orb's evolution to the judgment loop's, and the judgment loop is the part that must stay
easy to reason about.

## What would reverse this

- Design decides the guide orb must eventually **judge** something ("you have to keep up
  here to continue") - then it really is a `BeatTarget` variant and the two should merge
- Playtesters swing at guide orbs even with the separated palette → hue was not enough, and
  the divergence has to go further (ghostlier material, different silhouette or size) rather
  than anything changing about the code split

---

## When this is superseded

*(not yet superseded)*

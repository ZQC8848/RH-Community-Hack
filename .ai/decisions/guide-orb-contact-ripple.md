# The guide orb's activated surface is a ripple spreading from the contact point, not a hex grid

**2026-08-29 ・ status: standing ・ scope: the guide orb's activated surface treatment only - not its colour convention, its scale response, or its trail particles**

## What forced the choice

The guide orb's activated state had to be more than "the same sphere, brighter" - a
distinctly different surface pattern was wanted, so that reaching into an orb reads as
*activating* it rather than as standing near it. Candidates considered: scanlines, a
hexagonal energy grid, cracks, and concentric ripples.

A hex grid was the initial recommendation, and it was withdrawn before any shader work
started, for the reason below.

## Decision

**Concentric ripples spreading outward from the point where the hand entered.** The distance
field is `distance(worldPos, _ContactPoint)`; `frac(d * frequency - time * speed)` gives
rings travelling outward. As the hand moves inside the orb, the ripple origin moves with it.

New shader `Assets/Shaders/GuideOrb.shader`, keeping `BeatSphere.shader`'s Fresnel rim and
emissive core as the base layer with ripples composited over it. Driven by `_Excite`,
`_ContactPoint`, `_RippleFrequency`, `_RippleSpeed`, `_RippleWidth`, `_RippleColor`.

## Why not the alternatives

| Alternative | Its case | Why not |
|---|---|---|
| **Hexagonal energy grid** | Strong sci-fi read; sits naturally beside the ring's existing dashed segments | Both ways of laying a grid on a sphere have visible artefacts: **spherical UVs pinch at the poles** (the top of the orb visibly compresses), and **triplanar projection seams**. Each needs extra work purely to hide a defect. Ripples use a distance field - no UVs, no poles, nothing to hide |
| **Scanlines** | Cheap, very legible | Need an axis, and a sphere has no natural "up". Any choice looks arbitrary, and worse, looks *wrong* as the player walks around it |
| **Cracks / fractures** | Dramatic | Reads as damage. This is positive feedback for doing the right thing; the visual language should not say "broken" |
| **Just brighter (existing dials only)** | No new HLSL at all; ships today | Explicitly rejected in design discussion - a graded brightness change alone does not read as a state change |
| Ripple **from the orb centre** | Simpler, no `_ContactPoint` to feed | Loses the causal read. The whole point is that the pattern grows *from where you put your hand*, so the orb responds to you rather than merely reacting |

The deciding factor was not aesthetic preference. Ripples are both cheaper and more
expressive here: the contact point was already being computed to derive `excite`, so the
causal version costs one extra vector uniform, while the grid costs artefact-hiding work.

## What this rests on

| Assumption | Status | Evidence, or the check that would settle it |
|---|---|---|
| **Ripples stay legible on a 15cm orb in a headset** | **unverified - load-bearing** | At `rippleFrequency = 12` the rings may be too dense to resolve at arm's length and could alias into moiré under headset reprojection. Check in-headset early; the fix is lowering frequency, and if that does not work the pattern is wrong |
| One contact point is enough | **known limitation, accepted** | With two hands in one orb, only the higher-`excite` hand sources the ripple. A second source would need a second uniform and a second distance field. Not worth it until someone reports the single source looking wrong |
| Compositing over the existing Fresnel base reads well | unverified | The base is transparent by design (so the beat sphere's fade works). A bright ripple over a low-alpha core may wash out. First prototype settles it |

## Accepted costs

`_ContactPoint` is per-instance, so the orb's material properties must go through
`MaterialPropertyBlock` - already the project convention, so effectively free. A second
sphere shader to maintain, accepted in
[guide-orb-not-a-beat-target.md](guide-orb-not-a-beat-target.md).

The hex-grid look is not lost, only deferred: a grid layer can be composited over the
ripples later without changing this decision.

## What would reverse this

- Ripples alias or read as noise in-headset and lowering the frequency does not fix it
- The orb needs to read as activated **from across the room**, where a fine surface pattern
  resolves to nothing and a whole-orb treatment would win

---

## When this is superseded

*(not yet superseded)*

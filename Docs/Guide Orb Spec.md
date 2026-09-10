---
status: v1 (implemented and wired into DanceCaptureScene, 2026-08-29)
date: 2026-08-29
related: "Dance Capture Recording and Playback Spec.md" (same folder, recording and playback); "Ring-Sphere Judgment and Art Spec.md" (same folder, the beat target - a different thing from this document, see §1)
---

# Guide Orb Spec

Two orbs travel automatically along a recorded controller trajectory, emitting particles into a
trail as they go. When the player puts a controller inside an orb, ripples appear across its
surface, it swells, and its particles brighten.

Its purpose is **guidance and teaching**: to let the player see "this is how this passage moves",
and confirm with their body that they are keeping up.

## 1. It is not a BeatTarget

The easiest thing to get wrong is implementing this as a variant of
[BeatTarget](../Assets/Scripts/Interaction/BeatTarget.cs). Their time models are fundamentally
different:

| | BeatTarget (judgment sphere) | GuideOrb |
|---|---|---|
| Time model | Organised around **one instant**, `T_perfect` | **Continuously present**, no special instant |
| Lifecycle | Spawn → Approaching → Resolved/Expired → destroyed | Exists for the whole take, loops with it |
| Player state | Binary: touched / did not touch | Continuous: how close the hand is to the centre |
| Result | Perfect / Good / Miss-Touch / Miss-Timeout | No judgment result, only a follow-rate statistic |
| Failure | Yes (Miss) | **None.** Not keeping up simply means it is not lit; no penalty |

Every field on `BeatTarget` answers "how far is it from that instant". Forcing it to fit here would
produce a monster with junk values in every timing field.

**The reuse boundary is drawn at the art layer, not the logic layer**: the neon energy pulse
direction is shared, and so is the "right hand cool / left hand warm" colour relationship - but
**the hues and the particle material are their own set** (see §3.3), and the state machine is
written fresh, and is much smaller. Reasoning in
[../.ai/decisions/guide-orb-not-a-beat-target.md](../.ai/decisions/guide-orb-not-a-beat-target.md).

## 2. Layering: the orb does not know it is following recorded data

```
DancePlayer          (existing, DanceCapture/)  dsp clock, reference frame, sampling; writes the proxy's world pose
  └─ GuideOrb        (new, Interaction/)        sits on the proxy. Only knows "how close a hand is to me"
       ├─ visuals     scale / material / particles
       └─ HandTrail  (new, Interaction/)        draws the player's hand trajectory; lives on the LineRenderer object
DanceFollowScore     (new, DanceCapture/)       subscribes to both orbs' IsFollowing, accumulates the follow rate
```

`HandTrail` likewise knows nothing of orbs, hands or recorded data - whatever drives it calls
`Track(position, gateOpen)` once a frame, and it handles buffering, ageing and rendering itself.
This keeps `GuideOrb` from growing a second job beyond "judge contact".

`GuideOrb` **knows nothing whatsoever about "recorded data"** - it needs only its own transform and
two hand Transforms. That means it can sit on anything that moves (an object following a spline, an
energy orb trailing an NPC, and so on), in line with the standing principle in
[modular-portable-interaction](../.ai/decisions/modular-portable-interaction.md).

Knowledge like "how long is a take" and "which pass are we on" all stays in `DanceFollowScore`,
which is the component genuinely tied to the recording system.

**`DancePlayer` needs one change**: an `OnPassStarted` event. **Looping goes through `StartPass`**,
while `Play()` is only called once at the very beginning, so without it the follow rate cannot be
reset at a loop boundary.

### 2.1 Scene wiring (DanceCaptureScene)

The two orbs are **top-level objects**, and `DancePlayer`'s `leftProxy` / `rightProxy` point at
them.

**They must not be parented under the original box proxies** - those boxes have a localScale of
**0.08**, so an orb parented there shrinks to a twelfth of its size while the detection radius does
not, and the two come apart instantly (exactly the trap in §9.1). `GuideOrb` checks `lossyScale` in
`Update` and warns once if it deviates from 1.

The original box proxies were **deleted** on 2026-08-29 (confirmed as not coming back). They also
carried `TrailRenderer`s - with the particle trail and the hand trail line, the scene had three
separate trail concepts, one of which no longer had any role.

`DanceFollowScore` sits on the `Dance Capture` object (the same one as `DancePlayer`).

## 3. Activation: a fixed-radius threshold test

### 3.1 Why distance rather than a trigger collider

The existing [HandTouchSource](../Assets/Scripts/Interaction/HandTouchSource.cs) uses
`OnTriggerEnter`, which is an **enter event**; what is needed here is a **continuous inside/outside
state**, which is a different thing.

2 orbs × 2 hands = 4 distance comparisons per frame (squared distance, no square root) - negligible
cost, and it avoids the whole Rigidbody / isTrigger configuration minefield.

### 3.2 The test is binary and the radius is fixed

```
inside = distance(hand, orbCenter) <= ActivationRadius
ActivationRadius = orbRadius × scaleExcited        // the orb's radius at its largest, 0.15 × 1.35 = 0.2025 m
```

**Both halves matter:**

1. **Entering and leaving is a threshold, not a gradient.** Inside that radius is in, outside is
   out; there is no "deeper means brighter" in between.
2. **That radius is constant and does not follow the orb's current size.** The orb shrinks when
   idle, but the thing the player is aiming at **must not shrink with it** - otherwise the
   judgment range drifts along with its own visual feedback, and gets harder to aim at the closer
   you get.

> ⚠️ **This creates a coupling: `scaleExcited` is now a gameplay parameter, not just an art one.**
> Changing it moves the judgment boundary. This is commented in the code; know it before editing.

**Each hand is tested separately and the max is taken**:

- With the **correct hand** → target excite 1.0
- With the **wrong hand** → target capped at `wrongHandExciteCap` (default 0.35)

Taking the max is the only rule under which using the correct hand can never make the orb *dimmer*
(when both hands are inside the same orb at once).

**Smoothing affects the visuals only, not the test.** `excite` is smoothed toward the binary target
over time (attack 0.08s / release 0.25s), so the orb does not snap; but `IsFollowing` (and
therefore the follow rate) uses the **raw, unsmoothed boolean**.

> ⚠️ **A single threshold has no hysteresis.** A hand hovering exactly on the 0.2025 m boundary will
> chatter. The visuals absorb it through smoothing, but the follow-rate statistic will jitter - for
> a ratio metric that averages out, so the impact is small. Add separate enter/exit radii if it ever
> becomes a real problem.

### 3.3 The hand rule: the wrong hand is "refused", not "under-powered"

Guide orbs use **their own set of hues**: **green = right hand / amber = left hand**.

Beat targets are cyan (right) / magenta (left). The two deliberately do not share hues - **a guide
orb is not a target you may hit**, and if the two looked alike, players would very likely swing at
guide orbs (that risk is recorded in
[../.ai/decisions/guide-orb-not-a-beat-target.md](../.ai/decisions/guide-orb-not-a-beat-target.md)).

But **the "right hand cool, left hand warm" relationship is kept** (beat targets: cyan cool /
magenta warm; guide orbs: green cool / amber warm), so the left/right mapping reads consistently
across both systems and the player does not have to learn two rules.

| | Beat target | Guide orb |
|---|---|---|
| Right hand (cool) | cyan `(0.10, 0.50, 0.80)` | green `(0.20, 0.58, 0.18)` |
| Left hand (warm) | magenta `(0.55, 0.06, 0.48)` | amber `(0.70, 0.38, 0.05)` |

Either hand can light an orb, but the feedback differs **in kind** - otherwise the player cannot
distinguish "I used the wrong hand" from "I did not reach in far enough", since both would just look
like "less bright".

| | Correct hand | Wrong hand |
|---|---|---|
| Colour | Its own hue, brightened | **Desaturated** toward grey-white (`wrongHandDesaturation`, default 0.8) |
| Size | Swells | **Does not swell** (scale stays at its idle value) |
| Particles | Denser and brighter | Sparser and dimmer |
| Counts toward the follow rate | Yes | **No** (see §7) |

Desaturation is a **colour lerp, not shading logic**, so it stays on the C# side and the shader only
sees a single `_Excite` scalar. No second channel is needed in the shader.

## 4. Feedback mapping: one excite drives three things

All three feedback channels have a single underlying state, so there is never a "the orb swelled but
the particles did not" mismatch.

| Feedback | How it is driven |
|---|---|
| Size | `scale = lerp(scaleIdle, scaleExcited, correctExcite)`, correct hand only. **Idle 0.9, fully active 1.35** |
| Surface | `_RimIntensity` / `_CoreAlpha` / `_Excite` lerp from their idle values to their excited values |
| Particles | `startColor` / `startSize` / `rateOverDistance` lerp in step |

**The idle state is grey too, not just the wrong hand.** Desaturation takes the larger of the two:

```
greyness = max( idleDesaturation × (1 - excite),  wrongness × wrongHandDesaturation )
```

No hand at all is the most inert state, so `idleDesaturation` (0.9) is **deeper** than
`wrongHandDesaturation` (0.8). Alongside it, `rimIntensityIdle` drops to 1.2 and `coreAlphaIdle` to
0.18 - an idle orb is grey, dim and small, and only lights up and swells on activation.

Taking the max rather than summing guarantees that **approaching with the correct hand always
brightens the orb, and the wrong hand never does**.

**Only the hand trail line follows the orb's colour; the particles do not.** The line's
`startColor`/`endColor` are driven every frame by `GuideOrb.handTrail` - fully transparent at the
start, full colour at the end - so the line greys out with the orb. Details in §6b.

**Particles keep their own colour**, from the `startColor` authored on the particle system, never
rewritten at runtime - only the orb and the line grey out when idle.

> `GuideOrb` only knows "there is a line that should match my colour", and `LineRenderer` is a
> generic Unity type carrying no knowledge of the recording system - the module's portability is
> not compromised.

Material properties always go through a **`MaterialPropertyBlock`**, never `renderer.material`,
which would clone a material per instance. This is an existing convention in the project.

## 5. Surface style: ripples spreading from the contact point

On activation, **concentric ripples** appear on the orb's surface, **spreading outward from the
exact point the hand entered**. As the hand moves inside the orb, the ripple source moves with it.

The distance field is simply `distance(worldPos, _ContactPoint)`, and
`frac(d * frequency - time * speed)` gives the outward-spreading rings.

**Why not a hexagonal energy grid**: both ways of laying a grid onto a sphere have visible flaws -
spherical UVs pinch at the poles (visible compression at the top of the orb) and triplanar
projection has seams, and either would cost extra time to disguise. The ripple's distance field has
no UV problems and no pole artefacts, and it naturally expresses causality: the ripples grow out of
**the point where your hand went in**, rather than the whole orb lighting up uniformly. Full
trade-off in
[../.ai/decisions/guide-orb-contact-ripple.md](../.ai/decisions/guide-orb-contact-ripple.md).

A grid is additive and can be layered over the ripples at any time; do the ripples first and see.

**A new `Assets/Shaders/GuideOrb.shader`, rather than editing `BeatSphere.shader`.** Beat targets
and guide orbs are now two different things, and sharing one shader would have each side's
properties dragging on the other.

New properties:

| Property | Purpose |
|---|---|
| `_Excite` | 0..1, overall intensity, written every frame from C# |
| `_ContactPoint` | World position, the ripple's source. C# is already computing the distance for excite, so the contact point comes for free |
| `_RippleFrequency` / `_RippleSpeed` / `_RippleWidth` | Ripple density, speed, and the width of a single ring |
| `_RippleColor` | HDR, the ripples' own colour |

The Fresnel base follows `BeatSphere.shader` (rim glow + emissive core), with the ripples layered on
top.

## 6. Trail particles

A `ParticleSystem` with `simulationSpace = World`, using a single emission channel:
**`rateOverDistance` (3 → 60 per metre, following `correctExcite`)**.

Emission by distance rather than by time: with `rateOverTime`, an orb that stops moving piles
particles up in one spot; by distance, particle density follows **speed of motion** - fast passages
naturally get a denser trail, and that density is itself information about the dance, not just
decoration.

Idle 3/m against active 60/m is a **20×** contrast. The particles themselves are tiny (0.006 →
0.014 m), so the whole thing reads as fine stardust rather than dense smoke.

**Known trade-off**: emitting by distance alone means putting a hand into an orb that happens to be
**stationary** produces no particles at all. A time-based emission channel was added once to fill
that gap and **reverted on 2026-08-29** - the constant spray it produced was not the look wanted. If
the lack of feedback while stationary ever becomes a problem, this is the known fix.

Particle colour comes from the `startColor` authored on the particle system and is **not rewritten
at runtime**; it does not follow the orb's state (the orb and the path line grey out, the particles
do not).

The particle material is **its own copy**, `Assets/GuideOrbs/_Base/GuideOrbParticle.mat` (copied
from `HitBurstParticle.mat`), not shared with the beat target's - otherwise editing one would
quietly edit the other.

Lifetime 2.5s, `maxParticles` 500 (estimated as `60/m × 2m/s × 2.5s ≈ 300`). The cap must be tuned
alongside the rate - leaving a number far above real demand only hides how many particles this
system actually needs. The alpha curve holds and only fades at the end, and the size curve pops
slightly at birth then holds at 0.55 rather than shrinking to 0 - letting alpha handle disappearance
and size handle presence is what makes the particles read as lingering rather than fleeting.

## 6b. The hand trail line draws the player's hand, not the orb

**Since 2026-08-29 the two `LineRenderer`s (`Left Path` / `Right Path`) are driven by `GuideOrb` and
draw the player's actual controller trajectory**, not the recorded path the orb is currently
travelling.

Reasoning: the orb already performs the recorded trajectory, so drawing the same line again is
duplicate information. Drawing the player's own hand is the only new information that line can
provide - **what you actually traced**.

**Gating:**

| Moment | Behaviour |
|---|---|
| Hand enters the orb (correct hand) | Start recording the hand's world position each frame |
| Hand inside the orb | Keep recording |
| Hand leaves the orb | **Keep recording for `graceSeconds` (default 1s)** |
| Grace period ends | Stop recording; existing points age out over `pointSeconds` and the line drains itself |

The grace period exists because a hand sweeping quickly through an orb, recorded only during the
frames it is strictly inside, yields a dot rather than a stroke - a second of grace is what makes a
legible stroke.

**Point ageing**: each point is discarded after `pointSeconds` (default 0.6s), so line length is
bounded and does not grow without limit during a long activation. Raise it to make strokes persist.

`minDistance` (0.005 m) prevents hundreds of points piling up in one spot when the hand is still;
`maxPoints` (200) is a hard cap.

**This logic lives in its own component, `HandTrail`** (on the object with the `LineRenderer`), and
those four parameters live on it, not on `GuideOrb`. `GuideOrb` calls
`Track(hand world position, is the correct hand inside)` once a frame, plus `SetColor()`.

`HandTrail`'s work happens in **`LateUpdate`**: whatever drives it calls `Track` from its own
`Update`, and `Update` order between components is undefined, so only `LateUpdate` guarantees
reading this frame's value.

> **The code in `DancePlayer` that drew the recorded path has been removed entirely** (`leftPath` /
> `rightPath` / `pathTrailSeconds` / `pathResolution` / `BuildPathWindow`). Two components writing
> `positionCount` on the same `LineRenderer` means whoever wins depends on `Update` order - that is
> undefined behaviour, not two features coexisting.

### 6b.1 Material trap: URP/Unlit silently ignores a LineRenderer's colour

The two lines originally used **`Universal Render Pipeline/Unlit`** with an **Opaque** surface.

**`URP/Unlit` does not sample vertex colour**, and a `LineRenderer`'s
`startColor`/`endColor`/`colorGradient` are passed down precisely as vertex colours. So the earlier
change to "make the line follow the orb's colour" **did not take effect in a single line** - every
value on the C# side was correct, `lr.startColor` read back correctly, and the render was
nevertheless pure white; Opaque also made the alpha fade completely inert.

Switching to **`Universal Render Pipeline/Particles/Unlit`** (Transparent + straight alpha blending),
which does sample vertex colour, is what made the colour and the fade actually appear.

> **Lesson**: `LineRenderer` / `TrailRenderer` colours are vertex colours. Before assigning a
> material, confirm that shader samples vertex colour, or the code that sets colours will **quietly
> do nothing** - and the fact that every value on the C# side reads back correctly makes this
> especially deceptive.

## 7. Follow-rate statistics

Owned by `DanceFollowScore`, **not by GuideOrb** (§2).

- **What counts as "kept up"**: the correct hand within the activation radius (0.2025 m). That
  simple. Once the test is binary there is no "how deep counts" question left.
- **The statistic uses the raw, unsmoothed boolean** (`GuideOrb.IsFollowing`), not the smoothed
  `excite` that drives the visuals. Smoothing exists to look good; measuring whether the hand was
  actually inside should not inherit a 0.25s release tail.
- **Only the correct hand counts.** The wrong hand can light an orb but does not count - otherwise
  "either hand is fine" degenerates into "flailing is fine".
- **A rate per hand, plus a combined total.** A large difference between the two hands is itself
  useful information.
- Numerator = accumulated duration of frames meeting the threshold (`Time.deltaTime` is fine here;
  no audio alignment is needed); denominator = the take's trimmed duration.
- **Reset at the start of each pass**, via `DancePlayer.OnPassStarted` (§2).

`DanceFollowScore` is also responsible for calling `ClearTrail()` on both orbs at the start of each
pass (§9.2) - it is the only component that knows both where the orbs are and when a pass begins.

`DanceFollowScore` exposes numeric properties only; **displaying them is somebody else's job** - UI
belongs to `DanceCaptureUI`, keeping the module boundary clean.

## 8. Tunable variables

| Variable | Default | Notes |
|---|---|---|
| `orbRadius` | 0.15 | Visual radius, metres. Matches `BeatTargetConfig.sphereRadius`'s default |
| — | — | `activationRadiusMultiplier` was removed: the activation radius is fixed at `orbRadius × scaleExcited`, and multiplying by another factor would contradict the "equal to the orb at its largest" rule |
| `wrongHandExciteCap` | 0.35 | Excite ceiling for the wrong hand |
| `wrongHandDesaturation` | 0.8 | How far toward grey-white the colour goes for the wrong hand, 0..1 |
| `exciteAttack` | 0.08 s | Smoothing time as excite rises |
| `exciteRelease` | 0.25 s | Smoothing time as excite falls. Deliberately slower than attack |
| `scaleIdle` / `scaleExcited` | **0.9** / 1.35 | Size multipliers. Idle is smaller than the activation radius, see §9.1. Note the script's defaults are 0.5; the prefab holds 0.9, and the prefab's value is what runs |
| `idleDesaturation` | 0.9 | Desaturation with no hand present. Deeper than the wrong hand's 0.8 |
| `rimIntensityIdle` / `Excited` | **1.2** / 6.0 | Same dial range as `BeatSphere`. Idle is darkened |
| `coreAlphaIdle` / `Excited` | **0.18** / 0.55 | As above |
| `rippleFrequency` | 12 | Rings per metre |
| `rippleSpeed` | 2.5 | How fast the rings spread outward |
| `rippleWidth` | 0.15 | Width of a single ring, 0..1 |
| `_RippleFalloff` | 0.3 m | How the ripples fall off with distance from the contact point. Without it the whole orb flashes together and reads as "the orb is glowing" rather than "something is spreading from the contact point". Material-only, not in C# |
| `particleRateIdle` / `Excited` | **3** / **60** per metre | `rateOverDistance`, trail density. 20× contrast |
| `particleSizeIdle` / `Excited` | **0.006** / **0.014** m | Single particle diameter. Very fine against a 0.15m orb |
| `particleLifetime` | 2.5 s | How long they linger. Works with `maxParticles` to set the ceiling |
| `maxParticles` | 500 | Per orb. Estimated as `rate per metre × hand speed × lifetime`: active 60/m × 2m/s × 2.5s ≈ 300. **This number must be changed alongside the rate** |
| — | — | `followThreshold` was removed: with a binary test it is meaningless; `IsFollowing` simply means "the correct hand is inside the orb" |

## 9. Two traps already fallen into

**9.1 The detection radius and the visual radius must come from the same source - but they are not
identical.**

Since 2026-08-29 the orb scales with its state, so **the visible radius changes and the activation
radius does not**:

| | Radius |
|---|---|
| Activation radius (constant) | **0.2025 m** = `orbRadius × scaleExcited` |
| Visible radius, idle | `orbRadius × scaleIdle` |
| Visible radius, fully active | **0.2025 m**, exactly the activation radius |

**The activation radius is pinned to the orb's largest size**: when the orb shrinks while idle, the
thing the player aims at must not shrink with it, or the judgment range drifts along with the visual
feedback and gets harder to aim at the closer you get.

So while idle the orb is smaller than its own activation range - before the hand has "touched" that
small orb, it has already lit up and swelled to meet it, which reads as "the orb senses you coming"
rather than "you bumped into it". At full activation the two coincide exactly.

The invariant: **both are derived from the same `orbRadius`**, the activation one additionally fixed
by `scaleExcited`, the visible one by the current state factor. There must never be two separately
maintained numbers, where someone changes the visual size and forgets the detection.

The original trap: this project has been caught once already, with the ring radius 4× off the
sphere's real radius because a parent carried a scale and local scale multiplied all the way down
([record](../.ai/debug/2026-08-20-ring-radius-wildly-off-from-sphere-radius.md)). Here the two radii
**must** derive from the same serialized field, with the root kept at scale 1.

**9.2 Particles must be explicitly `Clear()`ed on loop.** `DancePlayer` calls
`SetProxiesVisible(false)` between loops, but **world-space particles do not disappear because their
object was hidden** - they live out their lifetime. Without clearing, the start of the next pass
shows the previous pass's leftover tail hanging in the air.

## 10. Explicitly out of scope

- **No judgment and no failure.** Not keeping up means the orb is not lit; there is no Miss. For
  rhythm judgment, use `BeatTarget`.
- **No haptic feedback.** Controller haptics are a separate feedback channel and deserve their own
  design (strength, duration, relationship to the music).
- **How the follow rate is presented.** This spec defines only how the number is computed, not where
  it is shown, what it looks like, or whether it affects difficulty.
- **Generating a beatmap automatically from recorded data.** That is sections 2.2 / 3 of the design
  doc, and has nothing to do with guide orbs.

# Current state

**Last updated: 2026-08-29**

## Dance capture (2026-08-26) — new subsystem

Records a real dancer's controller motion so takes can later be turned into beat charts.
Spec: [../Docs/Dance Capture 录制与回放规格.md](../Docs/Dance%20Capture%20录制与回放规格.md).
Reference-frame decision: [decisions/dance-capture-frozen-reference-frame.md](decisions/dance-capture-frozen-reference-frame.md).

- Code in `Assets/Scripts/DanceCapture/`: `DanceReferenceFrame`, `DanceSample`,
  `DanceRecording` (ScriptableObject), `DanceRecorder`, `DancePlayer`, `DanceCaptureUI`.
  Deliberately decoupled from `BeatTarget` and free of XR types — it is the *input* to a
  future beat extractor, not part of the gameplay loop.
- Scene: `Assets/Scenes/DanceCaptureScene.unity`, a copy of SampleScene with all beat logic
  **disabled rather than deleted** (BeatSpawner, both harnesses, both Beat Hit Volumes).
- **X on the left controller** (or keyboard X) starts a **3s countdown** then records; X
  again during the countdown cancels, X while recording stops and saves. **Hold B (right
  controller `secondaryButton`, or keyboard B) for 3s** to re-anchor the playback origin.
  Both bindings are code-created InputActions, not entries in the shared XRI action asset,
  so this dev tool cannot disturb the game's input map. English world-space UI is parented
  under the head and covers all five states (idle / countdown / recording / saved /
  recalibrating, the last with a hold progress bar).
- **Optional video (2026-08-28, reworked 2026-08-29)**: a `Video Screen` quad at
  `(0, 1.6, 2.5)` with a `VideoPlayer` rendering into
  `Assets/DanceCapture/VideoRenderTexture.renderTexture`. Assign a clip to the recorder's
  `Video (Optional)`; it plays while recording, is saved into the take, and replays on
  playback. `audioOutputMode = Direct` so the VideoPlayer keeps its own A/V sync.
  - **This machine's `VideoPlayer` needs ~18 seconds to deliver its first picture**, then
    plays in exact realtime with zero drift. Measured in an empty scene with audio off, so
    neither this project nor the audio pipeline is involved. That single fact explains the
    long-running "frozen on frame one, no sound" symptom: the old code called `Stop()` +
    `Prepare()` on every pass, and a 12.5s take looping restarted the 18s climb before it
    could ever finish.
  - **`Assets/Scripts/DanceCapture/DanceVideoScreen.cs` now owns the decoder** and enforces
    one rule: **never call `VideoPlayer.Stop()`**. Everything that wants to stop uses
    `Pause()` + seek (`Park()`). It warms the clip up at scene load, so the startup cost is
    paid while the dancer is still putting the headset on. `DanceVideoScreen.For(vp)`
    attaches it on demand - no scene wiring.
  - **Gate on `frame >= 0`, never on `isPrepared`.** `isPrepared` goes true ~18s before a
    picture exists; using it as the start gate is exactly what let the countdown begin
    against a video that had not started. An earlier "fix" that waited for `isPrepared`
    before the countdown was wrong for this reason.
  - **The load-bearing unverified assumption**: that pausing + seeking really is cheaper
    than Stop + Prepare. If it is not, the failure mode is slowness (the pass waits on
    `IsReadyFor`), not desync - turn off `DancePlayer.Restart Video Each Loop`. Watch the
    `[DanceVideoScreen] ready after Ns` log for the real number.
  - **Graphics API is pinned to Direct3D11** from the DX12 hypothesis, which was **never
    validly tested** (the probe window was shorter than the latency). Rollback:
    `useDefaultGraphicsAPIs = True`, order `[D3D12, D3D11]`.
  - **Video-to-motion sync is still NOT verified in a headset.** Music drift measures
    1.3e-05 s because `PlayScheduled` rides the dsp clock; `VideoPlayer` rides the *render*
    loop. Needs a headset check; if a consistent offset shows up, record the video's start
    offset into the take and compensate downstream.
  - Assigning both a music clip and a video with audio plays both at once.
- **Optional music**: assign a clip to the recorder and it is scheduled with
  `PlayScheduled` against the exact dsp timestamp the take starts at — measured drift
  between audio playhead and recording elapsed was **1.3e-05 s**. The clip reference is
  saved into the `DanceRecording` and replayed (offset to `inPoint`) during playback. Leave
  it empty for a silent take. Recording and playback use **separate AudioSources** so a
  looping preview can't fight the take being recorded for one source's playhead.
- **Playback origin is calibrated once and kept across loops** — it deliberately does NOT
  re-anchor each loop, so successive passes land in exactly the same place and are
  comparable. Verified: moving the head 5m left the proxies where they were; calling
  `RecalibrateOrigin()` then re-anchored them to the new head pose.
- **Mode is decided by one rule**: a recording assigned to `DancePlayer` = PLAY MODE (the
  recorder component is **disabled outright**); an empty slot = RECORD MODE. Owned by
  `DanceCaptureModeController`, deliberately *not* by the recorder — a component that
  disables itself stops running `Update` and could never re-enable itself. Assigning or
  clearing the slot mid-play works without leaving play mode. Verified both directions.
  - `DanceRecorder.StartCountdown()` also refuses when the component is disabled. Without
    that guard, an external `Toggle()` call stranded it in `CountingDown` with no `Update`
    to advance it, and it would fire a stale take the moment the mode flipped back. Found
    by testing, not by reading.
- Saved takes are **timestamped** (`Dance_2026-08-26_16-20-40.asset`) so repeated recordings
  never collide and the filename says when it was captured.
- Trim is `inPoint`/`outPoint` only — **non-destructive, never delete samples**. No
  time-scaling, by decision.
- **`maxSampleRate` (default 90 Hz) is load-bearing, not a nicety.** Sampling every frame
  in an uncapped Editor hit 847 Hz: a 15s take was a 2.9 MB asset. With the cap a 14s take
  is 155 KB. Effective rate is `min(frameRate, maxSampleRate)`.
- **Saving is Editor-only** (`AssetDatabase`). On-device recording would need a JSON or
  binary writer into `persistentDataPath`. Expected workflow is Link / Air Link.
- Verified in Play mode with synthetic data (no headset needed): capture→save→asset works;
  `TrySample` interpolation is exact against an analytic path (error 0); trim arithmetic
  correct; playback drives the proxies to positions that match hand-computed expectations
  (left proxy at y=1.958 vs expected 1.956); path preview builds 200 points.
- **Not verified: an actual headset take.** Frame capture from a real head pose, whether
  the X button binding fires on real Touch hardware, and whether the frozen frame holds up
  when a dancer turns mid-take all need a real device.


> This file **expires** — that is its job. Update it at the end of a working session.
> Anything meant to stay true belongs elsewhere; see the routing table in [README](README.md).

## Where things stand

- No interaction gameplay code exists yet. The judgment mechanic and its art direction
  are now fully spec'd in [../Docs/Ring-Sphere 交互判定与美术规格.md](../Docs/Ring-Sphere%20交互判定与美术规格.md)
  and [decisions/ring-art-direction.md](decisions/ring-art-direction.md) — ring is a
  billboard, art direction is "neon energy pulse" (Fresnel glow + emission), and there
  are now **three** judgment outcomes below Perfect/Good: `Miss-Touch` (touched but
  outside the Good window — sphere grows + fades out, no burst VFX) and `Miss-Timeout`
  (never touched — sphere+ring shrink away). Both rule boundaries from the draft (spec
  §3.1, §3.2) are confirmed as of 2026-08-20; `Miss-Touch`'s exact audio/haptic values
  are still open (see spec §2 note).
- Project scoped to single-player-for-now ([decisions/defer-multiplayer.md](decisions/defer-multiplayer.md)),
  with a portability requirement for whatever gets built ([decisions/modular-portable-interaction.md](decisions/modular-portable-interaction.md)).
- STYLY NetSync + Meta OpenXR SDK were fully installed, wired into the scene, and
  verified end-to-end (Play mode: server handshake succeeded, avatar spawned) — then
  fully reverted. The scene is back to the stock `Main Camera` / `Directional Light` /
  `Global Volume`.
- Design doc copied into the repo at `Docs/Idea：RH Community Hack — VR节奏音游交互范式（Ring-Sphere + 真人录制映射）.md`,
  kept in sync with the Obsidian source (source of truth is Obsidian; re-copy after
  editing there).

## Next step

Core judgment logic, neon shaders, and hit-burst VFX are all built and validated
(2026-08-20) — see "Where things stand" below. Next up, in rough priority order:

1. **Eyeball and tune the visuals in Play mode** — the shaders/VFX are verified to
   compile, spawn, and self-clean, but nobody has judged whether they *look* right yet.
   Tune via material properties in the Inspector (rim intensity, ring thickness/softness,
   burst counts), no code changes needed.
2. **Sound + haptics** — still entirely unimplemented; `BeatTargetConfig` has the AudioClip
   slots wired through `PlaySfx` but every clip is null, and haptics don't exist at all.
   Design doc discussion flagged these as contributing more to VR "打击感" than particles do.
3. **Real VR controller input** — a new, small `OnTriggerEnter`-based adapter calling the
   same `BeatTarget.TryTouch` API the keyboard harness uses. **Do not modify `BeatTarget`
   itself for this** — if it turns out you have to, that invalidates an open assumption in
   [decisions/modular-portable-interaction.md](decisions/modular-portable-interaction.md).

## Where things stand (implementation)

- Core module built at `Assets/Scripts/Interaction/`: `JudgmentResult` (enum),
  `BeatTargetConfig` (ScriptableObject, all spec §6 tunables), `BeatTarget` (the state
  machine from spec §1 - `Initialize(config, perfectTimeDsp)` / `TryTouch(touchTimeDsp)`
  / `OnResolved` event), `BillboardToCamera` (generic, reusable).
- Dev-only keyboard test adapter at `Assets/Scripts/Interaction/DevTesting/BeatTargetKeyboardTestHarness.cs`
  (`E` spawn, `Space` touch oldest) - drives `BeatTarget` through the same public API a
  real VR controller will later use, per spec §8. **Do not port the `DevTesting/` folder**
  when this module moves to another project.
  - Uses `UnityEngine.InputSystem` (`Keyboard.current`), not the legacy `Input` class -
    this project's Active Input Handling is set to Input System Package, and legacy
    `Input.GetKeyDown` throws `InvalidOperationException` at runtime. If a fresh keyboard-
    binding field ever gets added and shows a wrong default in the Inspector, check
    whether Unity mis-mapped a raw serialized int across an enum type change (this
    happened once to `spawnKey`/`touchKey` after changing them from `KeyCode` to `Key`).
- Prefab at `Assets/Prefabs/Interaction/BeatTarget.prefab` (sphere primitive + billboard
  Ring quad child), default config asset at `Assets/Data/BeatTargetConfig_Default.asset`.
  `BeatTargetTestHarness` GameObject is wired up and live in `SampleScene`.
- Art (2026-08-20): hand-written HLSL shaders at `Assets/Shaders/BeatSphere.shader`
  (Fresnel rim glow + emissive core) and `BeatRing.shader` (additive glowing annulus in
  quad UV space), materials at `Assets/Materials/Interaction/`. Written as HLSL rather
  than Shader Graph — a `.shadergraph` file is version-fragile JSON that can't be authored
  reliably through MCP, and the exposed material properties cover most of the iteration
  need anyway. Rebuild in Shader Graph if node-level editing is ever wanted.
  - The sphere shader is **deliberately transparent** (`Queue=Transparent`, honours
    `_BaseColor.a` via `[MainColor]`). This is load-bearing: `BeatTarget`'s Miss-Touch
    vanish fades alpha to 0 through `renderer.material.color`, which an opaque shader
    would ignore, making the sphere pop rather than fade.
  - `BeatRing.shader`'s `_RingRadius` (default 0.88) is where inside the quad's span the
    visible circle sits. `BeatTarget` **reads this off the material at Initialize** and
    scales the quad by `2 * radius / _RingRadius` to compensate, so retuning it in the
    Inspector keeps the drawn ring aligned instead of silently desyncing. Measured at the
    perfect moment: ring visible radius 0.15 vs sphere radius 0.15, 0% mismatch.
  - **Prefab hierarchy contract**: the `BeatTarget` root stays at scale 1; `Sphere` and
    `Ring` are independent children. Do not scale the root — see
    [debug/INDEX.md](debug/INDEX.md) for what that broke last time.
- Hit VFX (2026-08-20): `PerfectBurst.prefab` / `GoodBurst.prefab` in
  `Assets/Prefabs/Interaction/`, wired into the default config. Both self-destroy via
  `ParticleSystem.main.stopAction = Destroy` (no despawn script, nothing lingers).
  Verified in Play mode: Perfect and Good each destroy the sphere immediately and spawn
  exactly one burst, and the bursts clean themselves up afterwards.
- **Beat-type folders (2026-08-20)**: every beat flavour is self-contained under
  `Assets/BeatTargets/<Flavour>/` — prefab variant, sphere+ring materials, both burst
  VFX, and its config asset. `_Base/` holds `BeatTarget_Base.prefab` (structure only,
  deliberately neutral grey materials so the base is never silently "the cyan one") plus
  the shared `HitBurstParticle.mat`. Two flavours exist: `Cyan/` and `Magenta/`.
  - **Both are Prefab *Variants* of `_Base`, not copies** (verified:
    `GetPrefabAssetType` returns `Variant` for both). Structural changes to the base
    propagate automatically — confirmed by measuring each variant independently: root
    `lossyScale` 1, ring visible radius 0.15 == sphere radius 0.15, inherited without
    being reconfigured. **Add a third flavour as another variant, never a duplicate**, or
    the hierarchy contract has to be maintained in N places.
  - The two flavours differ in **sphere and ring** style, not just hue — Cyan is a solid,
    steady, softly-lit orb with an unbroken ring; Magenta is a hollow throbbing shell with
    a dashed, slowly-spinning ring. `BeatRing.shader` / `BeatSphere.shader` gained the
    style dials that make this possible (`_Segments`, `_DashCoverage`, `_SpinSpeed`,
    `_Pulse*`), so new flavours should be reachable by material tuning alone rather than
    new shaders.
  - **Burst VFX are deliberately identical across flavours except for colour** (verified
    param-by-param: count, speed, lifetime, size, gravity, shape, render mode all match;
    only `startColor` differs). Magenta's bursts were briefly given their own faster,
    stretched, higher-count style and that was reverted on request — keep hit bursts
    stylistically uniform and let the ring/sphere carry the per-flavour identity.
  - A flavour still needs *both* a prefab variant (look lives in materials on the prefab)
    *and* its own config asset (VFX prefab refs live in the config). Tolerable at two
    flavours; if they multiply, consider moving the VFX refs onto the prefab so one asset
    defines a flavour end-to-end.
  - Two flavours map naturally onto the not-yet-implemented "允许触发的手柄（左/右）"
    variable in spec §6 (Beat Saber-style per-hand targets), but nothing implements that
    yet — right now they are purely cosmetic alternatives, and the keyboard harness is
    wired to Cyan only.
- Validated in Play mode via `Unity_RunCommand` (not real key presses - MCP's sandbox
  blocks reflection, so the harness's own `Update()` wasn't exercised end-to-end, only
  `BeatTarget`'s logic directly): Perfect/Good/MissTouch classification all correct
  against config defaults, double-touch is correctly rejected, and a real-time (no config
  override) MissTimeout target correctly expired, ran its vanish animation, and
  self-destroyed. **Still needs a real manual test** - Play the scene, press `E` then
  `Space` at different delays, confirm the four outcomes look/log right and that ring
  billboarding doesn't jitter.

## Hand-specific beats (2026-08-20)

- `BeatHand` is a `[Flags]` enum so one type covers both "which hand touched" and "which
  hands this beat accepts". `BeatTargetConfig.allowedHands` carries the rule; Cyan = Right,
  Magenta = Left. A touch from the other hand resolves as **Miss-Touch regardless of
  timing** — deliberately, since hitting the wrong-coloured beat is a mistake rather than a
  near miss.
- `TryTouch(double, BeatHand)` is now the single entry point — there is no hand-less
  overload, so every input source must state which hand it is and the judgment logic never
  branches on input source.
- `HandTouchSource` (in `Interaction/`, not `DevTesting/` — it is real gameplay) is the
  physical-contact adapter: a trigger collider + kinematic Rigidbody that calls `TryTouch`
  on contact. It references **no XR types at all**, only a `Collider` and a hand label, so
  the module stays droppable into a project with a different rig.
- Scene wiring: a `Beat Hit Volume` child (5cm trigger sphere) under each of
  `Left Controller` / `Right Controller`. Deliberately a **child**, so XRI's own components
  on the controller roots are untouched.
- Keyboard harness now has separate `leftHandKey` (Q) and `rightHandKey` (Space) so the
  wrong-hand rule is testable without a headset. E spawns cyan, F spawns magenta.
- Verified in Play mode: all four hand×flavour combinations classify correctly, and the
  physical path was confirmed end-to-end via `Physics.Simulate` — `BeatTarget`'s own log
  shows `HandTouchSource:OnTriggerEnter` in the stack producing `Right hand, delta=0ms ->
  Perfect` and `Left hand, delta=0ms -> MissTouch (WRONG HAND)`.
- **Not verified: real controllers in a headset.** Without an XR device connected, XRI
  leaves `Left Controller` / `Right Controller` **deactivated** in Play mode, so the hit
  volumes are inactive and never fire. That is correct behaviour (no tracked hand, no hand
  judgment), but it means the actual in-headset contact — including whether the 5cm volume
  is the right size and sits in the right place relative to the physical controller — still
  needs a real device.
- The hit volumes were briefly disabled on 2026-08-20 to A/B a controller-tracking bug that
  turned out to be an XR input profile problem (see [debug/INDEX.md](debug/INDEX.md)). They
  are **re-enabled** — if beat hit detection ever silently stops working in VR, check these
  two GameObjects are still active before anything else.
- Latent: `XRInputModalityManager` on the XR Origin has `m_LeftHand` / `m_RightHand` set to
  `NULL`. A switch to hand-tracking modality would deactivate the controllers, showing
  nothing in their place *and* killing beat hit detection (the volumes live under the
  controllers). Supporting hand tracking means assigning hand visuals there and putting a
  `HandTouchSource` on each hand too.
- Two traps when writing tests against this, both of which produced misleading results once:
  - `Destroy()` is deferred to end of frame, so inside one synchronous `Unity_RunCommand`
    a Perfect-resolved target is *not* yet null. Judge outcomes by the `OnResolved` callback
    or `BeatTarget`'s own log, not by null-checking the GameObject.
  - `AudioSettings.dspTime` advances in real time while editor-side setup runs, so calling
    `Initialize(config, dspTime)` and *then* building test objects burns the 80ms perfect
    window before contact. Build everything first, fix the perfect moment last.

## Guide orbs (2026-08-29) - built, NOT yet tried in a headset

Two orbs travel a recorded take, trailing particles; reaching a controller into one makes
its surface ripple, its volume grow and its particles brighten. Purpose is **guidance and
teaching**, not judgment - there is no Miss.

Spec: [../Docs/Guide Orb 跟随引导球规格.md](../Docs/Guide%20Orb%20跟随引导球规格.md).
Decisions: [decisions/guide-orb-not-a-beat-target.md](decisions/guide-orb-not-a-beat-target.md),
[decisions/guide-orb-contact-ripple.md](decisions/guide-orb-contact-ripple.md).

Built and wired into `DanceCaptureScene`; **never run in a headset**, and never run at all
with a take loaded (the player's `recording` slot was empty when this went in - assign one to
see the orbs move). Verified statically: shader compiles and is supported, both orbs sit at
world scale exactly 1.00 with activation radius 0.150, hand/colour pairing correct, all
references wired.

Load-bearing points:

- **It is not a `BeatTarget`.** That type is organised around one moment; this one has none.
  Reuse is drawn at the **art layer** (neon direction, cyan-right/magenta-left, particle
  look), not the logic layer.
- **Detection is a per-frame distance test against a fixed threshold**, not a trigger collider
  and not a gradient. Inside `orbRadius * scaleExcited` (0.2025m) is "in", outside is "out".
  The radius is deliberately the orb at its LARGEST and does not shrink with the idle orb - the
  thing the player aims at must not drift along with its own visual feedback.
  **This makes `scaleExcited` a gameplay dial, not just a visual one.** Smoothing (attack 0.08 /
  release 0.25) applies to the visuals only; `IsFollowing` is the raw, unsmoothed boolean.
  Single threshold, so no hysteresis - a hand hovering exactly on the boundary will chatter the
  follow-rate statistic. Averages out in a ratio; add an enter/exit radius pair if it ever bites.
- **Either hand can activate an orb, but the wrong hand reads as *rejected*, not weak** -
  desaturated, no growth, sparser particles, capped at `excite 0.35`, and not counted toward
  the follow rate. Two hands in one orb: take `max`.
- **Layering matters**: `GuideOrb` (Interaction/, portable) does not know recorded data
  exists; `DanceFollowScore` (DanceCapture/) owns take length and pass boundaries.
- **`DancePlayer` needs one new event, `OnPassStarted`.** Loops go through `StartPass` while
  `Play()` runs only once, so without it a per-pass statistic has nothing to reset on.
- Files: `Assets/Scripts/Interaction/GuideOrb.cs`, `Assets/Shaders/GuideOrb.shader`,
  `Assets/Scripts/DanceCapture/DanceFollowScore.cs`,
  `Assets/GuideOrbs/{Green,Amber}/GuideOrb_*.{prefab,mat}`,
  `Assets/GuideOrbs/_Base/GuideOrbParticle.mat`.
- **Guide orbs have their own palette: green = right, amber = left** (beats stay cyan = right,
  magenta = left), and their own particle material copied from the beat burst rather than
  shared. Deliberately different hues so a guide orb does not read as a hittable beat - but the
  "right is the cooler hue" relationship is preserved across both systems, so the hand mapping
  still reads the same way.
- **Scene wiring**: the orbs are **top-level** objects that `DancePlayer.leftProxy/rightProxy`
  point at. They are NOT children of the old box proxies - those sit at **localScale 0.08**,
  which would shrink the orb to a twelfth of its activation radius with no error anywhere.
  `GuideOrb` warns once if its `lossyScale` leaves 1. The old boxes are **deactivated, not
  deleted**, matching how beat logic is handled in this scene.
- **The path lines now trace the PLAYER'S hand, not the recorded path** (2026-08-29). The buffering
  lives in its own component, **`HandTrail`** (`Interaction/`, on the LineRenderer's GameObject);
  `GuideOrb` only calls `Track(handPosition, gateOpen)` once per frame plus `SetColor()`.
  Recording starts when the owning hand enters the orb and continues until `graceSeconds` (1s)
  after it leaves, points ageing out after `pointSeconds` (0.6s). `HandTrail` works in
  **LateUpdate** - the driver calls `Track` from `Update`, and component Update order is
  undefined. The orb already performs the recorded path - drawing it again was
  duplicate information; the player's own hand is the only new thing a line can show.
  **`DancePlayer`'s path code was deleted outright** (`leftPath`/`rightPath`/`BuildPathWindow`/
  `pathTrailSeconds`/`pathResolution`) - two components writing `positionCount` on one
  LineRenderer is decided by Update order, which is not a feature.
- **Cleanup pass 2026-08-29** removed verified-dead members: `GuideOrb.SetHands`/`OwnerHand`,
  `DanceVideoScreen.Clip`/`Time`, `DancePlayer.OnPlaybackStarted` (invoked, never subscribed), and
  the `_PulseSpeed`/`_PulseAmount` shader dials (0 in every material, never written from C#).
  `GuideOrb.Excite`/`ContactPoint` became private fields, `IsCorrectHandInside` collapsed into
  `IsFollowing`, and `sphereTransform` was dropped because `orbRenderer.transform` is the same
  object. The disabled box proxies were **deleted** - they are not coming back.
- **Still unresolved**: `DanceFollowScore`'s seven output properties (`LeftRatio`, `RightRatio`,
  `OverallRatio`, the three `LastPass*`, `HasCompletedPass`) have **no reader anywhere**. The
  follow rate is computed every frame and goes nowhere, which also leaves that class's only live
  effect being the trail clearing it does at pass boundaries. Either surface it in
  `DanceCaptureUI` or drop the statistics and rename the class.
- **URP/Unlit silently ignores LineRenderer colours.** A LineRenderer's start/end colour and
  gradient arrive as *vertex colours*, and `URP/Unlit` does not sample them - it was also set to
  Opaque, killing the alpha fade. An earlier "tint the line with the orb colour" change therefore
  did **nothing visible** while every C# value read back correctly. Fixed by moving the line
  materials to `URP/Particles/Unlit` (transparent, straight alpha). Check the shader samples
  vertex colour before believing any LineRenderer/TrailRenderer colour code works.
- `DanceFollowScore` also clears both trails at each pass boundary - it is the only thing that
  knows both where the orbs are and when a pass starts.
- **The path `LineRenderer` is now a rolling window, not the whole take.** `DancePlayer` rebuilds
  it every frame over the last `pathTrailSeconds` (0.6s) ending at the playhead, with the point
  count scaled to the window's real length. Drawing the whole trimmed path once per pass gave a
  knot of line across the entire dance - it said where the hand had been, not where it was going.
- **Everything visible takes `rimColor`, not `baseColor`** - orb, particles and path line. The
  sphere reads as its rim (the bright part), so base-coloured particles looked like a duller,
  unrelated hue trailing a bright orb. The lines were plain white before this, left over from the
  box-proxy era.
- **Idle is grey, dim and small; activation is the only bright state.** Greyness is
  `max(idleDesaturation * (1 - excite), wrongness * wrongHandDesaturation)` - max, not sum, so the
  correct hand always brightens and the wrong hand never does. Particles and the path line are
  re-tinted every frame from the orb's *current* colour, so they grey out with it (the line via an
  optional `LineRenderer` reference on `GuideOrb` - a generic Unity type, so portability holds).
- **Visible radius varies; detection radius does not.** Detection is fixed at 0.2025m (the orb at
  full size); the visible orb is smaller while idle and grows to exactly meet it on activation. So
  the orb lights up and swells toward an approaching hand rather than waiting to be touched. The
  invariant that still holds is that both derive from `orbRadius` - never two independent numbers.
- Particles emit on `rateOverDistance` **only** (3/m idle -> 60/m activated, a 20x contrast),
  and are small: 0.006m idle -> 0.014m activated, against a 0.15m orb. Fine stardust, not smoke.
  A second `rateOverTime` "burst" channel plus outward `startSpeed` and velocity damping was
  added and then **deliberately reverted on 2026-08-29** - the continuous spray was not the look
  wanted. Known consequence of distance-only: a hand reaching into a *stationary* orb emits
  nothing. That is the known fix if it ever needs one.
- **Particle colour is authored, not driven.** The orb and the path line grey out with state;
  the particles keep their own colour. (An earlier version tinted them from the orb - reverted.)
- Lifetime 2.5s, capped at **1500** per orb. `rate × hand speed × lifetime` is ~1200 at the
  activated rate, so a tight cap would silently eat the trail.
- Ripples needed one dial the spec did not anticipate: **`_RippleFalloff`** (material only,
  0.3m). Without it the whole orb flashes at once, which reads as "the orb is glowing" rather
  than "something is spreading from where my hand is".

## In flight / undecided

- `Miss-Touch` judgment's exact sound cue and haptic strength/duration are unset —
  spec says "louder than Miss-Timeout, weaker than Good" as a principle, not a number
- Beat-detection-from-recorded-trajectory (the design doc's core mechanic) is unimplemented and unprototyped — biggest open unknown in the whole design doc
- Whether the prefab's public API needs to expose anything for future multiplayer (e.g. a hook remote avatars could use for hit detection) is unverified — see the open assumption in [decisions/modular-portable-interaction.md](decisions/modular-portable-interaction.md)

- **Do players mistake a guide orb for a hittable beat?** Partly addressed by giving guide
  orbs their own hues (green/amber vs the beats' cyan/magenta), but they are still glowing neon
  spheres of the same size and style. Unchecked in a headset
- Whether ripples stay legible on a 15cm orb in a headset, or alias into moiré, is unchecked

## Things that will bite you

- A fresh clone of this repo will not have `.claude/skills/fieldnotes/` populated by git
  yet if it's gitignored — check before assuming this skill is available elsewhere
- If STYLY NetSync ever gets reinstalled, expect the same package-removal-leftover-files
  issue in reverse (leftover files from *this* revert lingering) — see
  [debug/INDEX.md](debug/INDEX.md) for the mechanism

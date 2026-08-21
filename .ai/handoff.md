# Current state

**Last updated: 2026-08-20**

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

## In flight / undecided

- `Miss-Touch` judgment's exact sound cue and haptic strength/duration are unset —
  spec says "louder than Miss-Timeout, weaker than Good" as a principle, not a number
- Beat-detection-from-recorded-trajectory (the design doc's core mechanic) is unimplemented and unprototyped — biggest open unknown in the whole design doc
- Whether the prefab's public API needs to expose anything for future multiplayer (e.g. a hook remote avatars could use for hit detection) is unverified — see the open assumption in [decisions/modular-portable-interaction.md](decisions/modular-portable-interaction.md)

## Things that will bite you

- A fresh clone of this repo will not have `.claude/skills/fieldnotes/` populated by git
  yet if it's gitignored — check before assuming this skill is available elsewhere
- If STYLY NetSync ever gets reinstalled, expect the same package-removal-leftover-files
  issue in reverse (leftover files from *this* revert lingering) — see
  [debug/INDEX.md](debug/INDEX.md) for the mechanism

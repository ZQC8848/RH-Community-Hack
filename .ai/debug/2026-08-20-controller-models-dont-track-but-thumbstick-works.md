# Controller models don't follow your hands in VR, but the thumbstick still moves you

**2026-08-20 ・ cost: a headset session plus a round of wrong hypotheses ・ resolved by: fixing the XR input profile settings**

## Symptom

- In the headset the controller models sat frozen at the rig origin — from the player's
  viewpoint they read as *missing entirely* at first (they were down at foot level / inside
  the body), which is how this was first reported.
- **Thumbstick locomotion worked the whole time.** That is the confusing part: input was
  clearly being delivered, so "input is broken" felt ruled out.
- **Zero errors and zero warnings in the console**, in editor and in play mode.
- In the Editor everything inspected clean: both `Left Controller` / `Right Controller`
  active, every renderer under `* Controller Visual` enabled, `TrackedPoseDriver`
  `enabled = True`, `Physics.simulationMode = FixedUpdate`.

## Why it was hard

Two systems in this scene *also* legitimately deactivate or freeze controllers, and both
looked like promising culprits:

| Ruled out | On what evidence |
|---|---|
| `XRInputModalityManager` had switched to hand-tracking modality | It genuinely does deactivate the controller GameObjects — confirmed at runtime *without* a headset, where both went `activeSelf = False`. But it was not what was happening with the headset on. (Its `m_LeftHand` / `m_RightHand` are still `NULL` — a separate latent issue, see below.) |
| The `Beat Hit Volume` (trigger collider + kinematic Rigidbody) added under each controller for beat hit detection | Disabled both and re-tested — controllers behaved the same. A/B'd explicitly rather than reasoned about, precisely because a Rigidbody under a `TrackedPoseDriver`-driven transform is the kind of thing that *sounds* guilty. |
| `TrackedPoseDriver` disabled or unassigned | `enabled = True`, and its Position/Rotation/Tracking-State inputs all referenced real actions (`XRI Left/Position`, `XRI Right/Rotation`, …) |

The remaining plausible-but-wrong lead was `m_IgnoreTrackingState = False` on both drivers —
which really does make the driver refuse to write a pose when tracking state reads "not
tracked", and really does match the symptom. It was a mechanism, not the cause.

## What was actually true

The **XR input profile settings** were misconfigured (OpenXR interaction profile). Fixing
those in Project Settings resolved it — no scene or code change was needed.

That also explains the misleading part: the profile governs which device bindings resolve,
so it can leave pose bindings unresolved while other bindings still deliver input, which is
exactly why the thumbstick kept working and the pose never applied.

## Two-minute path next time

| Instrument | Reading that means this |
|---|---|
| **Project Settings → XR Plug-in Management → OpenXR → Interaction Profiles** | Check this *first* when pose is dead but buttons/sticks work. Wrong or missing profile for the actual headset is the cheapest thing to rule out and the easiest to overlook, because nothing logs an error. |
| Does *any* input work? (thumbstick, trigger) | If yes, the input system and action assets are alive — the fault is narrower than "input is broken", and is likely at the binding/profile layer, not the action-asset or component layer |
| `Left/Right Controller` `activeSelf` at runtime | `False` means `XRInputModalityManager` deactivated them (modality switch) — a *different* fault from "active but frozen at origin" |

## Lesson

"Some input works" does not narrow the fault to the components that consume input — device
bindings resolve per-control, so a profile problem can starve pose while feeding buttons.
Check the platform-level input profile before auditing scene components. And when a change
you made yourself is a plausible suspect, disable it and re-test rather than reasoning about
whether it could matter — the A/B took a minute and removed a whole branch of the search.

## Still open (unrelated to this fix)

`XRInputModalityManager` on the XR Origin has `m_LeftHand` / `m_RightHand` set to `NULL`.
If the headset ever switches to hand-tracking modality, it will deactivate the controller
GameObjects and there is nothing assigned to show or drive in their place — the player would
see nothing and, because `Beat Hit Volume` lives under each controller, beat hit detection
would stop working too.

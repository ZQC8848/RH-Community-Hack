# Both interactions live in one new scene, copied from the capture scene rather than replacing it

**2026-08-29 ・ status: standing ・ scope: how the two interactions coexist and how the mode switch works. Not what either interaction does**

## What forced the choice

The beat loop and the guide orbs had grown up in separate scenes. The player should be able to
choose between them in one place, and recording no longer needed to be part of that scene.

`DanceCaptureScene` turned out to be most of the answer already: it is a copy of SampleScene
with the beat logic *disabled rather than deleted*, so it held both halves, one switched off.

## Decision

**Copy `DanceCaptureScene` to `PlayScene`, strip the recording side from the copy, and leave the
original untouched.**

- `PlayScene` drops `DanceRecorder`, `DanceCaptureModeController`, `DanceCaptureUI` and the
  recording AudioSource. `Beat Mode` and `Guide Mode` groups make the switch one `SetActive`
  per side.
- A new `Assets/Scripts/Play/` layer (`RHCommunityHack.Play`) holds `PlayModeController` and
  `PlayModeUI`. It is the **only** code aware of both modules; `Interaction/` and `DanceCapture/`
  still know nothing of each other.
- `PlayModeUI` reads `DanceFollowScore`, which until now computed a follow rate every frame that
  nothing read.

**"The merged scene does not need recording" is not "the project does not need recording."**
Recording is how takes come to exist at all, and
[DANCE_RECORDING_GUIDE.md](../../DANCE_RECORDING_GUIDE.md) is a documented workflow handed to
outside collaborators. Converting the capture scene in place would have cut off the pipeline
that feeds both modes.

### Switching tears down three things

Each is state that outlives its owner being hidden, and each was a real leak:

| Torn down | Otherwise |
|---|---|
| Live `BeatTarget`s | Keep running their own state machine and resolve as Miss-Timeout after the mode has changed |
| The spawn timer | Keeps ticking |
| `GuideOrb.ClearTrail()` | World-space particles do not disappear when their emitter is hidden |

`TearDown()` runs *before* the `SetActive(false)`, so everything it touches is still live.

**There was a fourth: an explicit `player.enabled = !beat`.** `DancePlayer` owns its own hold-B
action, and left running it re-anchors and restarts playback in the middle of beat mode. That
line is gone, because `DancePlayer` now lives *under* the `Guide Mode` group and the grouping
switches it off. The rule generalises:

> **Anything that serves exactly one mode belongs inside that mode's group.** Grouping is not
> cosmetic here - it is the enforcement mechanism. A line in `SetMode` is a rule somebody has to
> remember for every component they ever add; membership of a group is a rule the scene applies
> by itself.

`DanceFollowScore` moved for the same reason: it only scores guide mode, and it had been running
its `Update` through beat mode scoring nothing. Its `OnEnable`/`OnDisable` subscription to
`DancePlayer` is symmetric, so deactivation is safe and its last-pass numbers stay readable.

`PlayModeController` must still live on an object the switch never deactivates. A component that
switches off its own object can never switch itself back on - the trap that
`DanceCaptureModeController` existed to work around in the first place. `Play Controller` now
holds that plus `PlayModeUI`, and nothing else.

### The take is chosen in exactly one place

`PlayModeController.take` pushes down to `DancePlayer` and `DanceRecordingBeatSource` on every
mode switch, so both of their own `recording` fields are left empty in this scene. The fields
stay on the components because both run standalone in other scenes.

This was not merely duplication. With all three pointing at the same asset, the guard in
`StartGuide()` - `player.Recording != take` - was permanently false, so **the push had never
run once.** The mechanism that is supposed to be authoritative was masked by the duplicate that
made it redundant, and would have executed for the first time on the day the two disagreed.

### Hold-B moved up a level

The same gesture must re-anchor a different owner per mode: `DanceRecordingBeatSource` in beat
mode, `DancePlayer` in guide mode. Neither knows the other, so the controller owns the gesture,
at **1s**. `DancePlayer` keeps its own 3s handling for the capture scene, behind a new
`handleRecalibrateInput` flag. Without that flag, guide mode would run **two** handlers at two
different durations and fire twice.

## Why not the alternatives

| Alternative | Its case | Why not |
|---|---|---|
| **Convert `DanceCaptureScene` in place** | No duplicate scene to keep in step | Destroys the recording entry point and orphans the collaborator guide. Takes are the input to everything here |
| Build up from `SampleScene` | It has the newest beat wiring | It has none of the guide-orb setup; the capture scene already had both halves, one merely disabled |
| A brand-new scene from scratch | Cleanest layout, no inherited cruft | Re-deriving the XR rig, hit volumes, orbs, trails and UI by hand for no benefit the copy does not already give |
| Put the controller in `DanceCapture/` | No third namespace | It depends on `Interaction/` too. A separate layer makes the dependency direction visible instead of quietly widening what `DanceCapture/` means |
| Toggle components instead of GameObject groups | Finer control | Far more references to keep correct, and the grouping is what makes the teardown auditable |

## What this rests on

| Assumption | Status | Evidence, or the check that would settle it |
|---|---|---|
| The four teardowns are the complete set | **verified in the editor, not in a headset** | Switched four times in play mode: zero leftover beats, particles or line points at every transition |
| Two scenes sharing structure will not drift apart | **unverified - the real cost of copying** | `PlayScene` and `DanceCaptureScene` share the XR rig, orbs and trails. A fix applied to one will not reach the other, and nothing enforces that. Watch for it |
| Recording is still wanted at all | assumed | If the project ever truly retires recording, the capture scene and its guide go together, and this record's main premise with them |
| 1s is the right hold length | unverified | Short enough not to feel stuck, long enough not to fire on an accidental press. Feel-test it |

## Accepted costs

Two scenes now contain near-identical rigs, and keeping them in step is manual. Accepted because
the alternative is losing the ability to record, which no amount of scene tidiness pays for.

## What would reverse this

- Recording is genuinely retired → `PlayScene` absorbs the capture scene and this copy stops
  being a copy
- The two scenes drift far enough to cause a real bug → extract the shared rig into a prefab or
  an additively-loaded scene, rather than continuing to copy

---

## When this is superseded

*(not yet superseded)*

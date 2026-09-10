---
status: v1 (implemented and wired into PlayScene, not verified in a headset, 2026-08-29)
date: 2026-08-29
related: "Ring-Sphere Judgment and Art Spec.md" (the beat target itself); "Guide Orb Spec.md" (guide orbs); "Dance Capture Recording and Playback Spec.md" (recording and playback)
---

# PlayScene Modes and Beat Chart Spec

The player picks one of two modes in the same scene: **Beat mode** (hit spheres generated from
recorded data) or **Guide mode** (put your hands inside orbs travelling along the recorded
trajectory). **X** switches between them, and both read the same take.

## 1. What each of the three scenes is for

| Scene | Purpose | Records? |
|---|---|---|
| `SampleScene` | Development test bed for beat targets | No |
| `DanceCaptureScene` | **The recording scene**, sent to collaborators | Yes |
| `PlayScene` | **The subject of this document**, two-mode play | No |

`PlayScene` is a **copy** of `DanceCaptureScene` with the recording side removed (`DanceRecorder`,
`DanceCaptureModeController`, `DanceCaptureUI`, and the recording AudioSource).

> **`DanceCaptureScene` is deliberately left as it was.** Recording is the entry point of the whole
> data pipeline - without it there are no takes - and
> [DANCE_RECORDING_GUIDE.md](../DANCE_RECORDING_GUIDE.md) is the complete walkthrough handed to
> outside collaborators. "The combined scene does not need recording" is not the same as "the
> project no longer needs recording".

## 2. Mode switching

### 2.1 Grouping

**One rule: anything that serves only one mode must live in that mode's group.** Grouping is not
just tidiness - it is the mechanism that enforces cleanup.

```
Play Controller          never disabled. Integration layer only
  ├─ PlayModeController
  └─ PlayModeUI

Beat Mode                group root, SetActive by mode
  ├─ BeatSpawner        → BeatSpawner + DanceRecordingBeatSource
  ├─ Left Combo Trail
  └─ Right Combo Trail

Guide Mode               group root, SetActive by mode
  ├─ Dance Player       → DancePlayer + DanceFollowScore
  │    └─ Music         playback AudioSource
  ├─ Left / Right Guide Orb
  └─ Left / Right Hand Trail
```

Both groups stay at identity (position 0, rotation 0, scale 1) - `GuideOrb` treats `orbRadius` as a
**world** radius, and a scaled parent makes it warn.

> **Both groups must be active when the scene is saved.** On load, every `Awake` runs first, and
> only then does `PlayModeController.Start()` switch one side off. A side saved switched off never
> gets its `Awake` at load time.

The hit volumes (`Beat Hit Volume`) are children of the controllers and cannot be sorted into a
group; the controller toggles them separately.

### 2.2 `PlayModeController` must live on an object that is never disabled

> **This is a hard constraint.** A component that disables itself can never re-enable itself. The
> project has already been caught by this once - `DanceCaptureModeController` exists precisely
> because of the same problem.

It sits on `Play Controller`, an object that is never switched off in either mode.

### 2.3 Three things that must be cleaned up on a switch

Each is state that outlives its owner being switched off:

| Cleanup | What happens without it |
|---|---|
| Destroy live `BeatTarget`s | They keep running their state machines and resolve one by one as Miss-Timeout after the mode has already changed |
| `BeatSpawner.StopSpawning()` | The timer keeps running |
| `GuideOrb.ClearTrail()` ×2 | World-space particles do **not** disappear when their emitter is hidden; they hang in the air |

`TearDown()` runs **before** `SetActive(false)`, so the objects are still alive when they are
cleaned up. In the other order, the cleanup reaches nothing.

> **There used to be a fourth: a manual `player.enabled = !beat`.** `DancePlayer` now lives inside
> the `Guide Mode` group, so the group toggle switches it off naturally and that line is gone. This
> is more than one line saved: the old line was **a rule that had to be remembered** - anyone adding
> a new guide-only component had to remember to add another line - whereas now it is enough to put
> it in the group.

### 2.4 `BeatSpawner.startDelay` means something different now

`StartSpawning()` re-applies `startDelay` on **every switch**. The original 5 seconds was meant for
"press Play, then put the headset on", but beat mode is never the startup mode, so those 5 seconds
only ever appear on a deliberate switch - switch over, then nothing happens for five seconds, which
reads as broken. Changed to **1.5s** (one spawn interval).

### 2.6 The video comes from the take, and plays in both modes

There is only one source of video: `take.video`. `DanceVideoScreen` has **no** clip field of its
own.

| Mode | Driven by | Behaviour |
|---|---|---|
| Beat | `PlayModeController.ApplyVideo()` → `DancePlace.PlayVideo(true)` → `PlayFreely()` | Nothing to align to, so it just plays |
| Guide | `DancePlayer` | `CueTo(inPoint)` at the start of each pass, in step with the take |

> **2026-09-01: the player now belongs to the stage, one per stage.** `PlayModeController` no longer
> holds a `DanceVideoScreen`; it forwards commands to the current stage instead, and on arrival it
> also pushes that stage's screen to `DancePlayer` (`player.Screen`) - otherwise guide mode would
> seek the previous stage's decoder. Reasoning in "Dance Place Spec" §6.0.

**A take with no video → the whole screen's Renderer is switched off**, rather than leaving a black
board. A RenderTexture nobody writes to keeps whatever was in it last, and that is exactly what made
"no video configured" look like "the renderer is broken" for so long.

> **The video must be started by `PlayModeController`, not by `DancePlayer`.** `DancePlayer` now
> lives in the `Guide Mode` group and is switched off in beat mode. Leaving it in charge of video
> means the screen is black when starting in the default beat mode - a regression introduced by the
> grouping refactor, found only by testing.

### 2.5 The take is specified in exactly one place

`PlayModeController.take` is the **only** place a take is specified, and it is pushed down on a mode
switch:

```
StartBeat()  → beatSource.SetRecording(take)
StartGuide() → player.LoadRecording(take)
```

So `DancePlayer.recording` and `DanceRecordingBeatSource.recording` **must be left empty** in this
scene. The fields themselves stay - those two components work standalone in `DanceCaptureScene` /
`SampleScene`, where they use their own fields.

> **Why this is not merely "redundant".** All three used to point at the same asset, which made the
> guard `player.Recording != take` in `StartGuide()` permanently `false` - **the distribution path
> never executed at all**. The mechanism that was supposed to matter was masked by the duplicate
> assignment, and would first have run on the day the two disagreed. It now really executes on every
> switch into guide mode.

## 3. Where the chart's positions come from

**Positions** come from the recorded data; **timing** is still a timer.

### 3.1 Sample at T_perfect, not T_spawn

> ⚠️ **This is the easiest thing in the whole mechanic to get wrong, and getting it wrong produces
> no error.**

`BeatTarget.Initialize(config, perfectTimeDsp)`: the hit moment is `ringLeadTime` (1.2s) after the
ring starts shrinking. So the sphere must be placed **where the hand was at T_perfect**:

```
perfectTimeDsp = spawn moment + ringLeadTime
sample time    = the recording time corresponding to perfectTimeDsp
```

Sampling at the spawn moment instead makes the entire chart **systematically 1.2 seconds ahead of
the dance**: the sphere appears where the hand was 1.2 seconds ago and then asks the player to be
there 1.2 seconds later. Everything looks fine on screen - the spheres move, the positions come from
real data - but it does not match the dance at all, and nothing reports an error.

`BeatPlacementSource.GetPlacements()` therefore names its parameter `perfectTimeDsp` deliberately,
and the comment spells out "this is the hit moment, not the spawn moment".

### 3.2 Handedness → the config decides the flavour

`BeatConfig_Cyan.allowedHands = Right` and `BeatConfig_Magenta.allowedHands = Left`; that mapping
already existed. So whichever hand a position was sampled from picks the flavour - **the code that
used to choose a colour at random has been deleted**, because the data itself determines the colour.

Only when `BeatPlacement.hand` is `Either` (the random-scatter mode) does it fall back to a random
flavour.

### 3.3 Two spheres per beat

Each beat takes both hands' positions at once and spawns one sphere for each. A 48.7s take at 1.5s
intervals → 32 beats × 2 hands = **64 spheres per loop**.

### 3.4 Reference frame: sample the player's head once at the start

Positions in a recording are **relative to the dancer's head at the time**, so
`DanceReferenceFrame.Capture(head)` anchors them to the player's head and the spheres appear "where
the dancer's hands were relative to their head". This adapts to wherever the player stands and
whichever way they face.

**Anchoring happens on the first placement, not in `Start()`**: XR head pose is invalid before the
first frame, and anchoring in `Start()` would pin the entire chart to the camera's placeholder pose.

### 3.5 Module boundaries

`BeatSpawner` is in `Interaction/` and, per a standing decision, may not depend on the recording
system; `DanceRecording` is in `DanceCapture/`. So there is a layer of abstraction between them:

```
BeatPlacementSource (abstract base, Interaction/)
  ├─ BeatSpawnArea            (Interaction/)  random scatter, kept for testing
  └─ DanceRecordingBeatSource (DanceCapture/) takes positions from a recording
```

An abstract `MonoBehaviour` rather than an interface, so it can be serialized and referenced as an
ordinary component in the Inspector.

## 4. Combo Trail: in beat mode the trail has to be earned

The same `HandTrail` as the guide orbs, but not given away - it grows only by playing well.

| | Change |
|---|---|
| Perfect | level **+2** |
| Good | level **+1** |
| Miss-Touch / Miss-Timeout | level **−1** |

Levels 0–5, with **both length and colour saturation** interpolated by `level / 5`. At level 0 the
trail is **cleared outright** rather than merely shortened - `HandTrail` has a one-second grace
period, and merely shortening would keep drawing the last stroke for another second.

Colours match the beat flavours: magenta for the left hand, cyan for the right, taken from the rim
colour (a line reads by its bright part, the same rule as the guide orbs). Low levels pull toward
grey; high levels reach the full hue.

The level **resets to 0** on entering beat mode - a trail carried over from another mode was not
earned this round.

The `BeatSpawner.OnBeatSpawned` event was added for this: the scorer subscribes to each sphere's
`OnResolved`, and the spawner does not need to know who is listening.

> ⚠️ **Gains are instant; losses are not.** `BeatTarget` resolves Perfect/Good synchronously inside
> `TryTouch`, but Miss-Touch and Miss-Timeout only fire `OnResolved` **after their vanish animation
> finishes** (about 0.25–0.3s). So the trail lengthens the instant you hit and shrinks half a beat
> late when you miss. This is not a bug - it is inherited from the beat target's existing design -
> but while testing it looks a great deal like "the penalty is not working".

## 5. Hold B to re-anchor

The same gesture has to land on **different objects** in the two modes - beat mode anchors
`DanceRecordingBeatSource`, guide mode anchors `DancePlayer`. Neither knows about the other, so the
gesture belongs to `PlayModeController`.

| Scene | Handled by | Duration |
|---|---|---|
| `PlayScene` | `PlayModeController` | **1s** |
| `DanceCaptureScene` | `DancePlayer` (unchanged) | 3s |

They are told apart by the `DancePlayer.handleRecalibrateInput` switch. **Without that switch**,
guide mode would have two handlers with two different durations, and one press would re-anchor
twice in a row.

Re-anchoring in beat mode **destroys the live spheres first** - they were placed in the old
reference frame, and keeping them would put two coordinate systems on screen at once.

## 6. Stage characters: one clip drives three people

**Each stage** has three `SuperFusionAncestor (1)` instances, driven together by that stage's
`DanceCharacterDirector` (on `Stage N/Dancers`). Nine across three stages, but only the occupied
stage is running at any time (see Dance Place spec §2).

| | |
|---|---|
| Model | `SuperFusionAncestor (1)`, 66,303 vertices / 1 SkinnedMesh |
| Clip source | `DanceRecording.characterAnimation`, pushed down by `PlayModeController` |
| Clip length | 29.93 s / 30 fps |
| Positions | `(-2.4, 0, 2.2)`, `(2.4, 0, 2.2)`, `(0, 0, 1.8)`, facing the player |
| Size | Model is 1.091 m tall, **scale ×1.639** → 1.788 m |

**No AnimatorController is needed.** The director feeds the clip straight into the `Animator`
through a Playables graph. Adding a fourth or fifth dancer is therefore just one more reference in
the array.

### 6.0 ⚠️ The model and the clip are unrelated; Humanoid retargeting bridges them

The clip comes from `LaunchPad_Compassion_Dance_test`, the model is `SuperFusionAncestor (1)` -
**two entirely unrelated skeletons**:

| | Clip source | Model |
|---|---|---|
| Root chain | `Newton/Root/Hips` | `mixamorig:Hips` |
| Legs | `LeftThigh / LeftShin` | `mixamorig:LeftUpLeg / LeftLeg` |
| Spine | Spine1→2→3→4 (4 joints) | Spine→Spine1→Spine2 (3 joints) |
| Bone count | 79 | 67 |

> **So both FBXs must be `Animation Type = Humanoid`, and every `Animator` in the scene must have
> the model's Avatar assigned.**

A Generic clip binds **by bone path name**, and the `mixamorig:` prefix alone makes every path miss;
not a single bone binds. Converted to Humanoid, the clip becomes muscle-space
(`clip.humanMotion == True`) and is independent of any particular skeleton.

⚠️ **All three of these fail silently - the figures simply stand still** - the same class of silent
failure as the shared playable in §6.1 and the duplicated take assignment:

1. Either FBX left un-set to Humanoid
2. `Animator.avatar` not assigned
3. The model was swapped but `DanceCharacterDirector.dancers` was not re-wired

When investigating, check `Animator.isHuman` and `clip.humanMotion` first; both must be `True`.

### 6.0b The textures are embedded in the FBX and must be extracted by hand

The model arrives pure white. That is **not a missing material - the textures are still locked
inside the FBX**. Unity does not expose embedded media as sub-assets until they are extracted
manually, so "the sub-asset list has no textures" proves nothing about whether textures exist.

Select the FBX → Inspector → **Materials** tab → **Extract Textures** → save to
`Assets/MotionCaptures/Textures/`. `Color` and `Normal` hook up automatically.

Two import settings still need fixing afterwards: `Normal.png` must be set to the **NormalMap**
type, and metallic / roughness must have **sRGB turned off** (they are data, not colour, and gamma
decoding them is wrong).

> **Metallic / roughness are deliberately not connected.** URP's `_MetallicGlossMap` wants metallic
> in R and smoothness in A **of the same image**; Tripo exports two separate maps, and gives
> roughness (the inverse of smoothness). Dropping the metallic map straight in means its opaque
> alpha is read as smoothness = 1 and the whole character turns to mirror. Connecting them properly
> requires a channel-packing pass first (metallic → R, 1−roughness → A). Until then the material is
> pure matte.

### 6.1 ⚠️ Each of the three must own its own playable

> **Sharing one `AnimationClipPlayable` across three `AnimationPlayableOutput`s does not work, and
> it does not report an error.**

The prettier-looking approach is one clip playable with all three outputs pointing at it through
`SetSourcePlayable` - synchronisation then being structurally guaranteed. **Measured: only the first
character moves**; the other two stay in bind pose and the console is spotless.

So it is now **one playable per person, fed the same time value each frame**. What is shared is the
**time**, not the playable:

```csharp
for (int i = 0; i < clipPlayables.Length; i++) clipPlayables[i].SetTime(t);
graph.Evaluate();
```

Measured: all three match bit for bit on `LeftForeArm` and `Hips.y`.

### 6.2 A PlayableGraph must be destroyed by hand

`PlayableGraph` is not garbage collected. Without a `Destroy()` in `OnDisable` / `OnDestroy` it
leaks, and keeps writing to every `Animator` it was bound to.

### 6.3 The dancers are not aligned to the take

The character clip is 29.93 s and the take is 48.67 s; the lengths do not match, so the director
loops on its own clock (measured: at take time 11.95 s the dancers were at 18.34 s). **The three are
strictly in sync with each other, but the group is not in sync with the take.** Aligning them means
first deciding how: trim the clip, retime it, or have the director follow
`DancePlayer.PlayheadSeconds`.

## 7. Tunable variables

| Variable | Location | Default |
|---|---|---|
| `spawnInterval` | `BeatSpawner` | 1.5 s |
| `startDelay` | `BeatSpawner` | 1.5 s (see §2.4) |
| `ringLeadTime` | `BeatTargetConfig` | 1.2 s, **also sets the sampling lead** |
| `sphereRadius` | `BeatTargetConfig` | 0.12 m |
| `loop` | `DanceRecordingBeatSource` | true; the take restarts when it ends |
| `placeLeft` / `placeRight` | `DanceRecordingBeatSource` | both on |
| `maxLevel` | `BeatComboTrail` | 5 |
| `perfectGain` / `goodGain` / `missPenalty` | `BeatComboTrail` | +2 / +1 / −1 |
| `maxTrailSeconds` | `BeatComboTrail` | 0.8 s |
| `lowLevelDullness` | `BeatComboTrail` | 0.85 |
| `recalibrateHoldSeconds` | `PlayModeController` | 1 s |
| `startMode` | `PlayModeController` | Beat |
| `loop` | `DanceCharacterDirector` | true; the character clip restarts when it ends |
| `dancers` | `DanceCharacterDirector` | 3 Animators |

## 8. Known problems

**8.1 Raw motion data is not the same thing as a good chart.** Measured across the take, hand-to-head
distance ranges from **0.18 m to 1.09 m**, where a normal arm's reach is about 0.3–0.8 m:

- 0.18 m → a sphere spawns less than 20 cm from the face; hard to hit and unpleasant
- 1.09 m → beyond arm's reach; unreachable without stepping

**There is deliberately no distance filter today.** To add one, place spheres only at sample points
between 0.3 and 0.75 m, and either skip that beat or clamp the position onto that shell.

**8.2 The penalty rate may be too harsh.** Two spheres spawn every 1.5 s, so missing them is −2 every
1.5 s, while climbing from 0 to 5 takes three Perfects. A few missed beats empties the trail. It
follows the rules, but the feel needs testing.

**8.3 The beats do not come from the music.** `Dance_1984Dancinginstreets` now has video attached
(with an AAC track, `audioOutputMode = Direct`), so there is sound in the scene; but `music` is still
none, and **beats are still generated purely by a timer** with no relationship to the music at all.
The other two takes still have neither audio nor video.

**8.4 Nothing is verified in a headset.** Mode switching, cleanup, UI and combo levels have only been
verified in the editor, through component state and real judgments.

## 9. Explicitly out of scope

- **Extracting rhythm from recorded data.** Timing is still a fixed-interval timer; only position
  comes from the recording. This is the core mechanic of sections 2.2 / 3 of the design doc, still
  unimplemented, and the biggest unknown in the whole design.
- **Distance filtering** (see §8.1).
- **Scoring and results.** `DanceFollowScore` only measures guide mode's follow rate; beat mode has
  no score beyond the combo level.

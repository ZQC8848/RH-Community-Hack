---
status: v10 (prologue with recorded narration; rigged dancing props; editor-verified, not verified in a headset, 2026-09-10)
date: 2026-09-01 (v1: 2026-08-31)
related: "PlayScene Modes and Beat Chart Spec.md" (the gameplay itself); "Dance Capture Recording and Playback Spec.md" (take data)
---

# Dance Place Stages and Standing Spec

The scene has **six stages** laid out along a **zigzag timeline**, 55.3 m apart. **The panel and the
gameplay only appear once you are standing at one.** Each stage is one era on the timeline.

## 0. The zigzag timeline (v4)

This shape was not picked at random; it is the script's own image (p. 2):

> "...we are standing at the beginning of a **zigzagging timeline** with multiple dates that starts
> underneath our feet."

Up to v3 the stages were an equilateral triangle and travel meant picking a dance; now they are a
line, and **moving along the line = moving through history**. That disagreement sat open for a long
time in `Wage Love Script to Implementation Map.md` §5, and v4 resolved it on the script's side.

### 0.1 The six stages (in date order - the order under `Dance Places` is the date order)

| # | Stage | Take | Content | Dome colour |
|---|---|---|---|---|
| 1 | `Stage 1 - Ancestors` | `Dance_1700sAncestraldances` | ⚠️ **empty** | deep red |
| 2 | `Stage 2 - 1964` | `Dance_1964Dancinginthestreet` | ⚠️ **empty** | gold |
| 3 | `Stage 3 - 1984` | `Dance_1984Dancinginstreets` | complete | bright cyan (hand-tuned) |
| 4 | `Stage 4 - 2016` | `Dance_2016compnoholdingback` | complete | dark green |
| 5 | `Stage 5 - 2017` | `Dance_2017Wiacwagelove` | complete | purple |
| 6 | `Stage 6 - 2024 MIT` | `Dance_2024Mitancestors` | ⚠️ **empty** | blue |

> **The three "empty" takes are deliberate placeholders.** They have no samples / video /
> characterAnimation; walking in shows only the dome, the panel and three dancers standing still,
> and the console logs a `[DancePlayer] No recording assigned` warning - an honest "content missing"
> signal, not a bug. Each one's `label` field says which page of the script it corresponds to and
> what material it needs. Filling one in means attaching things to that SO; the stage does not
> change.

### 0.2 Positions and facing

X alternates between 0 and 36, Z increases by 42 per step:

```
  Stage 1 (0,   0)   Stage 2 (36,  42)   Stage 3 (0,  84)
  Stage 4 (36,126)   Stage 5 (0,  168)   Stage 6 (36, 210)
```

Neighbour spacing is **√(36²+42²) = 55.32 m**, satisfying "at least 50". The whole line spans 210 m.

**Each stage faces "the direction you walked in from"** (`LookRotation(pos[i] - pos[i-1])`; the
first borrows the first leg's direction), so the screen is always straight ahead as you come along
the line. Along the zigzag, yaw alternates between **40.6° / 319.4°**. This also decides where guide
orbs and beat targets land (§4's "facing comes from the stage").

### 0.3 The ground was enlarged

The original `Plane` was only ±50 m and ended under the third stage. It is now at position
`(18, 0, 105)`, scale `(14, 1, 32)`, covering x −52..88 / z −55..265.

## 1. Player loop

```
Empty ground (no panel, no gameplay)
  → a 2-metre white line at your feet zigzags off into the distance   ← this is the wayfinding
  → six domes of different colours are strung along it
  → get there (⚠️ how is currently missing, see §5)
  → come within 6 m: the panel lights up, the screen starts playing video, the dancers start dancing
  → play beats / follow the guide orbs
  → walk away → this round's score is cleared
```

**The line is the song-select screen, and also the narrative.** Walking replaces a menu - the most
valuable thing about this design; do not layer a song-select UI on top of it. Since v4 it carries a
second meaning too: **walking forward = moving to a later era**.

> ✅ **v7: the travel gap is filled, but not with teleport anchors.** The player no longer chooses
> where to go - `TimelineDirector` carries them along the timeline: dwell → dome dissolves → white
> line grows → travel to the next stage. See §14.

## 2. Composition: one set of gameplay + six sets of scenery

> **Only the scenery is duplicated; there is one set of gameplay.**

| | Copies | Contents |
|---|---|---|
| **Scenery** (`DanceStage.prefab`) | ×6 | Dome, round floor, video screen + **its own VideoPlayer**, dancers ×3, standing anchor (`Standing Anchor`), world panel |
| **Gameplay** | ×1 | `BeatSpawner`, guide orbs ×2, combo trails ×2, hit volumes, `DancePlayer` |

> ⚠️ **v4 took the scenery from 3 copies to 6, so skinned characters went from 9 to 18. All 18
> SkinnedMeshRenderers are instantiated at scene load**; load time and memory on a standalone headset
> must be re-measured. See §11.5.

### 2.3 Dancers render by distance (v5)

**Switching the dancers on and off is decoupled from "standing" and is now purely distance-based.**

| | Radius | Owned by |
|---|---|---|
| Dancer rendering | **20 m** (`dancerRenderRadius`, +3 m hysteresis) | `DancePlace.UpdateDancerProximity` |
| Standing (panel, gameplay, video) | enter 6 m / exit 8 m | `DancePlaceManager.Resolve` |

**Why the render radius must be larger than the enter radius.** Tied to standing, the dancers would
appear at the same instant as the panel - three people materialising six metres away, which reads as
"popping in". At 20 m you see them dancing from outside the dome first, then walk in. **Runtime cost
is unchanged**: stages are 55.3 m apart and the midpoint is 27.7 m from each, so at most one stage is
within 20 m at any time - still three dancers.

What gets switched off is **the whole GameObject**, so the `SkinnedMeshRenderer`, the `Animator` and
the director's `PlayableGraph` all stop - not just one draw call saved.

**The 3 m hysteresis is required**: standing on the boundary would make the director rebuild its
playable graph every frame.

> **Ownership was straightened out along the way.** The dancers used to be driven by
> `PlayModeController` through `SetRecording(take)` / `SetRecording(null)`; they now belong entirely
> to `DancePlace`. The `characters` field on `PlayModeController` was removed outright - dancers are
> not mode-specific, so it had no reason to know they exist.
>
> Side effect: **the dancers do not stop the instant you leave a stage**. Look back and you can still
> see them dancing until you are 23 m away. That feels more real than the old "turn around and
> they're gone".

### 2.1 A stage is a prefab; instances differ only by `take`

`Assets/Prefabs/DanceStage.prefab`. Adding another stage, in full:

```
drag into the scene → position it → set take to a different DanceRecording → add it to Stage Timeline's stops → done
```

Standing detection needs no wiring and no registration: `DancePlaceManager` finds every `DancePlace`
in the scene itself in `Start()` (see §3.3). **The white line is the only thing registered by hand**,
because date order cannot be inferred (see §5.2).

**Measured (v4, six stages): each instance has only two overrides against the prefab, `DancePlace`
and `Transform`** - that is, the take plus its placement.

> **This is an invariant to defend.** If you find yourself hand-editing something else on an instance
> - the key threshold, the poster, the dome colour - that setting belongs on `DanceRecording`, not on
> the instance. The moment stages start drifting individually, the prefab was pointless.

So **all** of the following moved from `DancePlace` to `DanceRecording`:

| Field | Why it belongs to the take, not the stage |
|---|---|
| `poster` | It is the poster for that video |
| `chromaKey` / `keyColor` / `keyThreshold` / `keySmoothness` / `spillRemoval` | Properties of how that shoot was lit; they travel with the footage (see §6.2) |
| `domeMaterial` / `domeColor` | What that era's venue looks like |

> Why not a separate "StageProfile" asset? Because that would be **back to two mount points** -
> exactly what moving the take from `PlayModeController` to `DancePlace` set out to fix. One stage =
> one SO, and only one.

### 2.2 Why the gameplay is not in the prefab

| Reason | |
|---|---|
| Gameplay is bound to the **hands**, not to the stage | Guide orbs, combo trails and hit volumes are children of the controllers, and there is only one pair of hands |
| **A prefab cannot serialize scene references** | Three copies of gameplay would each have to `Find` the rig at runtime, trading today's explicit wiring for implicit lookups |
| Three copies of state would need switching off three times | Today it is "swap the reference frame" and the objects follow by themselves |

Guide orb and beat target world positions come from `DanceReferenceFrame`, not from a parent
transform. Changing stage = changing the reference frame, and the objects follow by themselves.

Cost of three copies of gameplay (estimated from current assets):

| | One set | Three sets |
|---|---|---|
| `BeatSpawner` | 1 | 3 |
| Guide orbs | 2 | 6 |
| **Dancers (1 SkinnedMesh / 66.3k vertices / 52 bones)** | 3 | **9** |

Dancers are scenery and must be per-stage; but **stages you are not at must be stopped as a whole
group**. Nine skinned characters running at once on a standalone headset costs far more than three
RenderTextures (about 10.5 MB) - **if something must be saved, save this first, not the video**.

> ⚠️ **After switching to `SuperFusionAncestor`, a single dancer went from 20.5k to 66.3k vertices,
> 3.2× the original.** Only one stage is active at a time, so what actually runs is 3 dancers at about
> 200k vertices (previously about 60k). **This number must be measured on a standalone headset** - it
> is currently the most expensive item in the whole scene.

## 3. Standing detection: distance + hysteresis, not trigger boxes

Each frame, compute the head's distance to each stage's standing point and pick the nearest one
within radius.

```
enter radius  6 m
exit radius   8 m     ← must be larger than the enter radius
```

### 3.1 Why not trigger boxes

A large box has concrete traps in **this** scene:

```
The XR Origin has a CharacterController                    → triggers OnTriggerEnter
Each controller has a Beat Hit Volume (isTrigger)          → also triggers
```

When the player steps in, **both hands are also inside the box**, producing three Enter events.
Worse, `Beat Hit Volume` is activated and deactivated with the mode - **every mode switch fabricates
an Enter/Exit pair**. Using a box would mean filtering by component type or layer.

Distance has none of these problems: three comparisons, negligible cost, separate enter/exit radii
(a trigger box needs two nested boxes for hysteresis), and problems are visible at a glance in the
Inspector.

### 3.2 `DancePlaceManager` must not live on an object that can be disabled

> The same hard constraint as `PlayModeController`: a component that disables itself can never
> re-enable itself. It sits alongside `PlayModeController` on `Play Controller`.

### 3.3 Stages are discovered, not registered

The `places` array is **left empty**; `Start()` collects every one with `FindObjectsByType<DancePlace>`.

Stages are now prefab instances and will be duplicated casually. A hand-maintained list means
"duplicate a prefab, then remember to come back and register it", and **forgetting to register does
not error - that stage simply never activates**. That kind of silent failure is exactly what code
should eliminate.

If the array is not empty it is used as given, for deliberately excluding some stages. A warning is
logged if the scene has none at all.

## 4. Reference frame: position from the player, facing from the stage

This is **the easiest thing in the whole feature to get wrong**.

Today `DanceReferenceFrame.Capture(head)` takes **both position and facing from the player's head**.
A stage is a fixed place in the world, and the screen and dancers face a fixed direction - using it
as-is goes wrong:

| Anchoring | Result |
|---|---|
| All from the player (current) | Player arrives facing away from the screen, and every sphere spawns behind them |
| All from the stage | Always faces the screen, but if the player is not standing exactly on the designer's mark, every sphere is offset |
| **Position from the player, facing from the stage** ✅ | Spheres are always "between the player and the screen", and fit where they actually stand |

```csharp
// DanceReferenceFrame already has a public constructor; the struct itself needs no change
new DanceReferenceFrame(head.position, place.StandingRotation)
```

What needs changing is `DancePlayer` and `DanceRecordingBeatSource`: they currently **hard-code**
`Capture(head)` and need to accept a reference frame from outside.

> Footprints / a facing marker need to be on the floor. If the player does not know which way to
> stand, the anchoring logic being correct is no help.

## 5. Travel: 🔴 currently missing

**v3 removed teleporting from the stage prefab** (as requested: "delete everything teleport-related
inside the prefab"). What was removed:

| Removed | What it did |
|---|---|
| The whole `Beacon` GameObject | The pillar itself: `MeshFilter` + `MeshRenderer` + `CapsuleCollider` + `TeleportationAnchor` |
| The `DancePlace.beacon` field and its toggle logic | Switched the pillar off entirely when standing at the stage |

**Kept** is the empty `Teleport Anchor` object itself (renamed **`Standing Anchor`**) and its
`Standing Marker` / `Facing Arrow` children. The "teleport" name was historical baggage; it is
really §4's **standing position and facing**, `DancePlace.standingAnchor` points at it, and deleting
it would take the whole anchoring with it.

### 5.0 The gap this left (filled by auto progression in v7, see §14)

The scene **no longer contains any `TeleportationAnchor` or `TeleportationArea`** (measured: only
the rig's `TeleportationProvider` and `ClimbTeleportInteractor` remain, neither with anywhere to
land). So:

- The teleport ray can still be fired, but **never hits a valid destination**; pressing does nothing
- The only way to move is the **thumbstick**: about a dozen seconds of pushing to cover 24 m, and
  long continuous locomotion in VR makes people sick

**This must be filled, or of the three stages only the one you spawn at is actually playable.**
Possible fixes (none done):

| Direction | Notes |
|---|---|
| Put a `TeleportationArea` on the floor | Cheapest, but landing is free-form; players may stand off-mark and §4's facing anchor stops working |
| Put `TeleportationAnchor` back on `Standing Anchor` | Keeps precise landing and facing, but that is adding back exactly what was just removed |
| A different way to travel | The script wants "moving forward along the timeline"; maybe it should not be teleporting at all - see `Wage Love Script to Implementation Map.md` §5 |

### 5.1 Wayfinding now relies on the white line and the domes

The pillars doubled as cross-room landmarks. With them gone, the landmarks are **the white line at
your feet** (§5.2) plus **six domes of different colours** (§8.5). The domes are far larger than the
pillars, and reversed winding lets you see straight in from outside, so you can tell from a distance
what is playing inside; the white line answers the question the pillars never could - **which way is
the next one**.

### 5.2 The white line: `Stage Timeline`

Scene root `Stage Timeline`, with a `LineRenderer` + `StageTimeline` component.

| | |
|---|---|
| Width | **2 m** (`width`, written straight into `widthMultiplier`) |
| Height | y = **0.015** (`height`) |
| Material | `Assets/Materials/StageTimeline.mat`, URP/Unlit pure white |
| Folds | **3 more** inserted between each pair of stages (`foldsPerLeg`), offset **9 m** alternately left and right (`foldAmplitude`) |
| Points | 6 stages + 15 intermediate folds + lead-in and lead-out = **23 points** |
| Corners | `numCornerVertices = 6` |

**A `LineRenderer` rather than a generated mesh**: all the difficulty of a zigzag is in the corners,
and `numCornerVertices` solves them for free; and the line **follows the stages** - with
`[ExecuteAlways]`, dragging any stage makes the line follow immediately, no asset to regenerate.

The lead-in and lead-out extend along the first / last segment's direction, so the tails stay on the
zigzag rather than veering off - note that since v5 "the first segment" means the first **fold**,
not the first stage.

#### 5.2.0 Intermediate folds (v5)

Folding only at the stages would leave a 55-metre straight line between each pair, which reads as "a
connecting line" rather than "a path". Each leg now has 3 more folds, alternating 9 m left and right:

```
lead-in ─╮   ╭─╮   ╭─ Stage2 ─╮   ╭─╮   ╭─ Stage3 ...
      Stage1 ╰─╯   ╰─         ╰─╯   ╰─
```

**The left/right sign counts continuously across legs**, rather than restarting each leg. Restarting
per leg would put two folds on the same side either side of a stage, and the zigzag would stutter
there.

The offset direction is `Cross(up, leg direction)` - **perpendicular to that leg and horizontal** -
so folds only step sideways and never climb. Folds fall at t = 0.25 / 0.5 / 0.75, at least 13.8 m
from the nearest stage, so **they never cut into an 8 m dome**.

#### 5.2.1 The white line runs **under** the floor

```
stage root +0.03   Facing Arrow      facing arrow
stage root +0.02   Standing Marker   standing disc
stage root +0.01   Dome Floor        dome floor   ← covers the white line
stage root +0.005  white line                     ← only 5 mm either side
stage root  0      ground
```

**The white line passes under each stage's floor**, so it is visible only on the open ground between
stages and is covered as soon as it reaches a disc - which makes each disc read as a station on the
line.

Measured: looking straight down, nothing shows through anywhere inside the floor; looking along the
ground from 180 m away, no z-fighting either.

> ⚠️ **`StageTimeline.height` is relative to each stop, not a world coordinate.** It was once an
> absolute 0.015; later the whole scene was raised to y=0.1, and **the line sank 85 mm below the
> ground, completely invisible, with nothing to say why**. Made relative, it follows the ground
> however it moves.
>
> **Set it above +0.01 and the line climbs on top of the floor.**

The white line is 2 m wide and passes right through the standing disc (1.6 m), so the two
necessarily overlap. **The disc on top is correct** - it tells you where to stand. Measured: this
5 mm gap shows no z-fighting on the dome floor.

#### 5.2.2 ⚠️ The material must be double-sided

`StageTimeline.mat` has `_Cull = 0` (Off).

**This trap has been fallen into, and its symptom is extremely misleading.** Under `TransformZ`
alignment the ribbon's front face points **down**, so an ordinary back-face-culled material **draws
nothing from any angle** - not from above, not from ground level. Meanwhile the component reports
the correct point count, width and bounds, and the console is clean, so every wrong hypothesis looks
equally plausible. The first investigation suspected "the 0.015 m height gap is being eaten by depth
precision" and re-captured with the line raised to 0.6 m - the screenshots were **byte-identical**,
which is what ruled out depth.

**If the line ever disappears again, check `_Cull` first.**

### 5.3 Travel options must recompute their distances

The three fixes listed in §5.0 cost differently since v4: stage spacing went from 24 m to **55.3 m**.
Restoring parabolic teleport, `Velocity` 18 reaches at most 34.4 m - **not far enough to reach the
next stage**. Either raise `Velocity` again (roughly 25+ would be needed) or switch to continuous
travel along the line, which actually suits the script better (p. 1: the timeline advances by itself
if you stand still, and you can also teleport down it to speed up).

---

## 5-old. Teleport and beacons (v2 and earlier, removed, kept for reference)

### 5.1 Range is set by `Velocity`, not `Max Raycast Distance`

The teleport ray is an `XRRayInteractor` with `Line Type = ProjectileCurve`.

> ⚠️ **On a parabola, range is determined only by `Velocity`.** `Max Raycast Distance` applies only to
> `StraightLine`, and `End Point Distance` only to `BezierCurve` - both look like "distance", and
> changing either has no effect at all.

The original value of 10 reached at most **11.5 m**, and only 5.4 m aimed level - **nowhere near the
next stage 24 m away**. Changed to **18**:

| Velocity | Aimed level | 30° up | Max range |
|---|---|---|---|
| 10 (original) | 5.4 m | 10.8 m | 11.5 m |
| **18 (current)** | 9.6 m | 30.9 m | **34.4 m** |

### 5.2 The anchor is a pillar, switched off entirely as the player approaches (removed)

Each stage had a light pillar 0.6 m thick × 3.5 m tall, serving as both the teleport target and a
landmark visible across the room.

```
Teleport Anchor        empty object, transform = standing position + facing
  ├─ Beacon            the pillar: MeshRenderer + Collider + TeleportationAnchor
  ├─ Standing Marker   floor disc
  └─ Facing Arrow      facing arrow
```

> **`TeleportationAnchor` is on the pillar, not on the parent.** One `SetActive(false)` then switches
> off rendering, collision and the teleport target together, rather than toggling each inside
> `DancePlace` - which also means `DancePlace` needs no reference to XRI at all.

It switches off on §3's enter 6 m / exit 8 m. Since it vanishes as the player reaches 6 m, **they can
never bump into it**, and there is never a "teleport to where you are already standing".

Bonus: **a pillar makes the parabola much easier to aim**. Hitting a 2×2 m floor patch needs precise
elevation; hitting a 3.5 m upright pillar counts wherever along its height the arc grazes it.

### 5.3 The anchor itself (partly kept: the empty object renamed to `Standing Anchor`)

`Locomotion → Teleportation` on the rig was already enabled.

**The anchor's transform is at the same time this stage's standing position and facing** (§4's
`StandingRotation`); do not maintain a separate standing point - two copies of the data will
inevitably disagree one day.

Pushing the thumbstick across 20 m takes about 10 seconds, and long continuous locomotion in VR
makes people sick. Teleport is the right answer for comfort; the thumbstick stays for fine
adjustment of where you stand.

## 6. Video: poster ↔ live, **one decoder per stage**

Every stage has a permanent screen, and **every stage carries its own `VideoPlayer`**.

| State | The screen shows |
|---|---|
| Not standing | **Poster** (a static `Texture2D`) |
| Standing, decoder warming up (about 1.7 s) | **Still the poster** |
| Standing, picture available | RenderTexture (live) |

The key is the second row: **keep showing the poster while warming up, not a black screen**. The
1.7 seconds become entirely invisible, and the player perceives it as instant.

### 6.0 Why from "one decoder taking turns" to "one per stage"

v1 had one `VideoPlayer` in the scene, borrowed in turn. There were two reasons to change it:

**One: changing a single decoder's clip is as good as warming it up again.** `Park()` is cheap
precisely because it leaves the clip alone; a shared player **changes clip every time you change
stage**, paying the warm-up from scratch every time. With one each, a stage you have visited is
"paused where it was", and walking back gives a picture on the next frame.

> Measured (editor Play mode): stand at Stage 1 → walk to Stage 2 → walk back to Stage 1; on return
> `LiveTexture != null` held immediately, and the screen never went black or fell back to the poster.
> The three RenderTextures have different instance IDs (`-18724 / -19082 / -19938`), confirming none
> is shared.

**Two: making it a prefab requires it.** A stage that carries its own screen but borrows a player
from the scene is not a self-contained prefab.

The cost is three decoders and three RTs - but **allocated lazily**: a RenderTexture is created at
the clip's real size on the first `WarmUp`, and stages never visited create none. Measured: the
third stage's `targetTexture` is `none` until it is stepped onto.

> ⚠️ **Prefab instances must never share one RenderTexture asset.** All three players would decode
> into the same pixels, and the screen would show "whichever wrote last". Hence `DanceVideoScreen`'s
> rule: **if `targetTexture` is empty, create a private one**; if it is filled in, use what was given
> (which is how the recording scene uses it, unchanged).

### 6.0b Audio across stages

`audioOutputMode = Direct`, so all three players can produce sound - but **stages not being stood on
are `Park()`ed (paused) and silent**. Only the current one plays.

> **There is no poster artwork yet; all three takes' `poster` fields are empty.** With nothing
> assigned the screen hides entirely (rather than showing another stage's leftover frame, which is
> correct), so screens are not visible from a distance - **wayfinding relies on the beacons**
> (§5.2), not on posters. Adding posters would only make the distant view nicer; it is not a
> functional prerequisite.
>
> Posters now live on `DanceRecording`, not on `DancePlace` (§2.1).

The existing `DanceVideoScreen` rule is unchanged: **never `Stop()`**; changing stage uses `Park()`
(pause + seek).

> ⚠️ **There is a landmine here.** Those 1.7 seconds depend entirely on videos being **transcoded to
> VP8 on import**. On this machine H.264 through the OS decoder measured **55 seconds** to a first
> frame. The day someone drops in an untranscoded mp4, "entering a stage" becomes nearly a minute of
> frozen poster, and **no error is reported**.
>
> Every new video must be: select the `.mp4` → Inspector → tick Transcode → Codec **VP8** → Apply.
> (For the Quest build, each clip additionally carries an Android override with transcoding off, so
> the headset hardware-decodes the original H.264 - see `.ai/handoff.md`, "Quest 3 build".)

### 6.1 Looping

`DanceVideoScreen.loop` (on by default). Looping happens inside the decoder and **does not go through
`Stop()`, so no second warm-up is paid**.

Beat mode requires it: nothing there ever restarts the video, so without looping the screen freezes
on the last frame when the clip ends (the shortest current clip is only 4.5 seconds). Guide mode
seeks at the start of each pass itself and is unaffected.

### 6.2 Green-screen chroma key (at runtime)

`Assets/Shaders/VideoChromaKey.shader`. The parameters live on the **take** and are pushed to the
screen material by `DancePlace` through a MaterialPropertyBlock (§2.1).

**The test is done in the CbCr chroma plane, not in RGB.** Chroma distance ignores luminance, so the
lit highlights and the shadowed parts of a backdrop are keyed out together; RGB distance treats them
as different colours and leaves the shadows behind.

Measured (`Video2`, linear space):

| Region | Chroma distance from the background |
|---|---|
| Background (two samples) | 0.000 / 0.024 |
| Fluorescent green top | **0.229** - looks green, but safe |
| Black trousers | 0.215 |
| **Grey floor** | **0.166** - the thing that actually constrains the threshold |

So the threshold must fall **between 0.03 and 0.15**. Suggested starting point: threshold 0.08 /
softness 0.05.

> ⚠️ **4:2:0 is the ceiling of this approach.** Video chroma is half resolution, so hair, fingers and
> motion blur are judged from smeared chroma data. If the edges are not good enough, **key before
> compression and store with alpha** (VP8+alpha WebM, tick `Keep Alpha` in Unity) - the material
> and scene side need no rework.

> **The "one decoder per stage" listed as an alternative in v1 was adopted in v2**; reasoning in §6.0.

## 7. Clearing on leaving

Walking out past the exit radius (or teleporting elsewhere) **voids the current round**:

| Cleanup | Notes |
|---|---|
| Destroy live `BeatTarget`s | Reuses the existing `TearDown()` |
| `BeatSpawner.StopSpawning()` | Same |
| `GuideOrb.ClearTrail()` ×2 | World-space particles do not disappear when hidden |
| `BeatComboTrail.SetLevel(0)` | Existing public API |
| **Void this pass's follow rate** | ⚠️ see below |
| Video `Park()`, screen back to the poster | |
| This stage's dancers stopped as a group | |

> ⚠️ **`DanceFollowScore` currently has no "void" path.** It only has `FinishPass()`, which
> **commits** a result (writing `LastPass*`). Calling it directly would record an unfinished pass's
> partial data as a result. A discard path is needed: clear the accumulators but **not** write
> `LastPass*` or set `HasCompletedPass`.

## 8. Panel: fixed in the world, one per stage

The panel stands beside each stage's screen and **no longer follows the head**.

Currently `Play UI` sits under `Main Camera` (head-locked) and follows you everywhere - which
conflicts with the "walk up to a station" design.

There is still **only one** `PlayModeUI` (on `Play Controller`, never disabled); the manager points
its output at the current stage's `Text`. Three panels, one writer.

**When not standing anywhere there is no panel at all** - deliberately. Wayfinding relies on the
three domes (§5.1), not on panels or posters.

## 8.5 Domes and round floors

Each stage is wrapped in a sphere with **only inward-facing surfaces** (`ImmersiveSphere.prefab`,
radius 8 m, centred 0.01 m above the stage origin), with a round floor inside meeting the sphere
wall (`ImmersiveFloor.prefab`; `ImmersiveDomeFloor` adapts its radius by `r = √(R²-h²)`).

The principle is in `.ai/decisions/` (reversed winding - not normals, not a shader) and in the two
generators' comments. Only three stage-related points are recorded here:

**One: you can see in from outside.** With reversed winding, the near hemisphere is back-face
culled, so from Stage 1 you can see into Stages 2 and 3. **With the beacons removed, this went from
"a nice side effect" to the only basis for wayfinding** (§5.1).

**Two: the dome has no collider.** With one, any ray aimed across stages would hit its own stage's
wall. With the beacons gone there is temporarily nothing that needs to be aimed at, but **as soon as
a travel method is added this becomes a prerequisite again** (§5.0), so do not casually give the
dome a collider.

> Measured in v2 (beacons still present): a ray from Stage 1 at hand height (0, 1.2, 0) toward Stage
> 2's beacon, `RaycastAll` returned **1 hit**, the beacon itself (23.71 m). Neither side's dome wall
> nor either floor was in the way.

**Three: the sphere is centred 0.01 m up, not at 0.** The floor sits at y = 0.01: above the ground
`Plane` (y=0) and below the standing disc (y=0.02). Separated by 1 cm each, to avoid z-fighting.

The 8 m radius lines up exactly with §3's enter/exit radii (6/8): **reaching the edge of the dome =
leaving this stage**.

The dome colour comes from the take's `domeColor` (§2.1). Note that it **replaces** the material
colour rather than multiplying it - multiplying from `ImmersiveSphere.mat`'s blue base can never reach
warm brown or purple, so it replaces, with that blue as the default. If the material changes,
remember to change the default.

## 9. Which existing code changed

| File | Change |
|---|---|
| `DancePlayer` | Accepts an external reference frame instead of hard-coding `Capture(head)` |
| `DanceRecordingBeatSource` | Same |
| `DanceFollowScore` | New "void this pass" path (§7) |
| `PlayModeController` | `take` changes from a serialized field to being pushed in at runtime by the stage; `Mode` gains a `None` (not standing) state |
| `PlayModeUI` | Switchable output target |
| `DanceVideoScreen` | Material switch between poster ↔ RT |
| **New** `DancePlace` | One stage: take, screen, dancers, anchor, panel |
| **New** `DancePlaceManager` | Picks the current stage each frame and handles entering/leaving |

### v2 (2026-09-01, prefab conversion)

| File | Change |
|---|---|
| `DanceRecording` | New "Stage presentation" block: `poster`, the five chroma-key fields, `domeMaterial`, `domeColor` (§2.1) |
| `DanceVideoScreen` | When `targetTexture` is empty, creates a private RT at the clip's size, released in `OnDestroy`; new `ClipAspect` (§6.0) |
| `DancePlace` | Gains `videoScreen` / `domeRenderer`; chroma key and poster read from the take; screen adapts to the video's aspect |
| `DancePlaceManager` | Discovers stages automatically; no longer holds the scene-wide `VideoPlayer`; `EnterStage` now passes the whole `DancePlace` |
| `PlayModeController` | `videoScreen` field removed, video handed to the current stage; on arrival pushes the stage's screen to `DancePlayer` |
| `DancePlayer` | New runtime `Screen` property; falls back to the serialized `videoPlayer` when empty (which keeps the recording scene unaffected) |
| **Removed** the scene-root `Video Source` | Each stage has its own |
| **Removed** the scene-root `Immersive Sphere` | Its contents moved into the stage prefab |

### v3 (2026-09-01, teleport removed)

| File | Change |
|---|---|
| `DanceStage.prefab` | Removed `Beacon` (including `TeleportationAnchor` + `CapsuleCollider`); `Teleport Anchor` renamed `Standing Anchor` |
| `DancePlace` | Removed the `beacon` field and the beacon line in `SetOccupied` |
| **Untouched**: the rig's `TeleportationProvider` / `Teleport Interactor` | Only the prefab's parts were removed; the ray is still there, it just has nowhere to land (§5.0) |

### v4 (2026-09-01, six stages + zigzag timeline)

| File | Change |
|---|---|
| **New** `StageTimeline` | The white line: strings the stages together in the given order, follows them under `[ExecuteAlways]`, warns when spacing is too small |
| **New** `Assets/Materials/StageTimeline.mat` | URP/Unlit pure white, **`_Cull = Off`** (required, see §5.2.2) |
| **New** three placeholder takes | `Dance_1700sAncestraldances` / `Dance_1964Dancinginthestreet` / `Dance_2024Mitancestors`, all empty except `label` and `domeColor` |
| `PlayScene` | Stages 3 → 6, rearranged into a zigzag and each turned; `Plane` enlarged to x −52..88 / z −55..265; new `Stage Timeline` |
| **Untouched**: `DancePlace` / `DancePlaceManager` / the prefab | All six stages are instances of the same prefab; not one line of code changed |

### 9.1 The take's single source moves to the stage

`PlayModeController.take` is currently a serialized field, "the one place a take is chosen". After
the change **that field disappears**, and the take comes from `DancePlace.take`, pushed in on arrival.

The single-source rule is unchanged; only the question changes: **at any moment only one stage is
active, and its take is the answer.** `DancePlayer`'s and `DanceRecordingBeatSource`'s own
`recording` fields must still be left empty.

## 10. Tunable variables

| Variable | Location | Suggested default |
|---|---|---|
| `enterRadius` | `DancePlace` | 6 m |
| `exitRadius` | `DancePlace` | 8 m (must be > enter) |
| `take` | `DancePlace` | **Different per stage - the only thing to change on an instance** |
| `poster` | `DanceRecording` | Different per stage (currently all empty) |
| `loop` | `DanceVideoScreen` | true |
| `chromaKey` | `DanceRecording` | true |
| `keyColor` | `DanceRecording` | Different per stage, sampled from the actual picture |
| `keyThreshold` | `DanceRecording` | 0.08 (must fall within 0.03–0.15, see §6.2) |
| `keySmoothness` | `DanceRecording` | 0.05 |
| `spillRemoval` | `DanceRecording` | 0.7 |
| `domeColor` | `DanceRecording` | Different per stage (1984 warm brown / 2016 cyan / 2017 purple) |
| `domeMaterial` | `DanceRecording` | Empty (uses the prefab's own) |
| `fitScreenToVideo` | `DancePlace` | true |
| `dancerRenderRadius` | `DancePlace` | 20 m (must be > enterRadius, see §2.3) |
| `dancerRenderHysteresis` | `DancePlace` | 3 m |
| `width` | `StageTimeline` | 2 m |
| `height` | `StageTimeline` | 0.015 (only 1 cm of headroom, see §5.2.1) |
| `foldsPerLeg` | `StageTimeline` | 3 |
| `foldAmplitude` | `StageTimeline` | 9 m |
| `leadIn` / `leadOut` | `StageTimeline` | 24 m |
| `minSpacing` | `StageTimeline` | 50 m (warns below it) |
| `stops` | `StageTimeline` | **In date order**; the only hand-maintained list |
| ~~`Velocity`~~ | the controller's `Teleport Interactor` | Still 18, but **there is currently nothing to land on** (§5.0) |

## 11. Known problems

**11.5 Dancer load cost - one optimisation pass done in v6, see §12.** Scene Transforms down from
1368 to 198, meshes down from 13.22 MB to 10.19 MB. **Still not measured on a standalone headset.**

**11.6 🔴 The triangle budget is the real problem, and it is untouched.** A single dancer is 91,258
triangles × 3 = **273,774 triangles**, while a standalone headset's whole-frame budget is usually
100–200k - **the dancers alone exceed it**, before counting domes, screens, UI and green-screen video.
66.3k vertices is the raw density of a Tripo-generated model, 5–10× too high for background
characters 8–20 m away. Either decimate to 10–15k or add a LODGroup. **That means changing art
assets, which was outside the scope of this optimisation.**

**11.0 Travel - solved in v7 by auto progression (§14).** The player is carried along the line and no
longer chooses. **The cost is that "walking is song select" is gone**: this version is a purely linear
tour. The script wants both (p. 1: the timeline advances by itself if you stand still, and you can
teleport down it to speed up), so a manual path can be added back later, but it does not exist now.

**11.1 Aspect ratio - handled automatically, but never verified with real footage.**
`DancePlace.fitScreenToVideo` scales the quad to the clip's real aspect ratio, **inscribed** within
its original size (portrait footage becomes a narrow 1.01×1.80 screen rather than being squashed).

⚠️ **All three takes currently carry 1920×1080 video, so in testing this logic is the identity
transform (all three screens remain 3.20×1.80) - meaning it has never really been exercised.** The two
612×1088 portrait clips, `Video2` / `Video2-2`, are not attached to any take yet; the day they are,
the first thing to check is whether the screen narrows.

**11.1b The grey floor cannot be keyed out.** `Video2` / `Video2-2` have a grey-white floor under the
dancers rather than green screen, so keying the background leaves a grey slab at their feet. Its
chroma distance of 0.166 is too close to the person (black trousers 0.215), and raising the threshold
to remove it would eat the person too. The feasible fix is a **bottom fade** in the shader. `Video1`
has an all-green floor and does not have this problem.

**11.1c Colour metadata is missing on the new footage.** All five new videos produced
`Color primaries 0 is unknown ... may result in color shift` on import. This means **the green seen
in the engine may differ from the source file**, so key colours must be tuned against the actual
picture rather than copied from values measured in external tools. Re-muxing with ffmpeg to write
bt709 metadata fixes it at the root.

**11.2 The dancers are still not aligned to the take.** The character clip is 29.93 s, the take
48.67 s, and the director runs on its own clock. The three stages are each independent, and the
problem is unchanged (see PlayScene spec §6.3).

**11.3 Mode selection is still the X key.** Choosing a mode on the panel would be more intuitive,
but that is a separate matter, outside this spec.

**11.4 Nothing is verified in a headset.** Teleport landing, the standing/facing cue, and how the
6/8 m enter/exit radii feel all have to be tested with a headset on - the radii especially, which
cannot be judged at all while developing seated.

## 12. Dancer load optimisation (v6)

Everything changed was an import setting on `Assets/MotionCaptures/SuperFusionAncestor (1).fbx` (now
`SuperFusionAncestorV4.fbx`, same guid) - **not a line of code nor a scene was touched** (import
settings live in the `.meta`).

| Setting | Before | After | Gain |
|---|---|---|---|
| **`Optimize Game Objects`** | Off | **On** | **67 → 2** Transforms per dancer |
| `Import BlendShapes` | On | Off | That mesh has `blendShapeCount = 0` |
| `Import Cameras` / `Import Lights` | On | Off | Nobody uses the FBX's cameras and lights |
| `Skin Weights` | 4 bones/vertex | **2** | Mesh **13.22 → 10.19 MB** |

Measured:

```
Scene Transforms    1368  →  198      (dancer bones had been 88% of them)
Per dancer          67    →  2        (Animator root + SkinnedMeshRenderer)
Mesh                13.22 →  10.19 MB
```

### 12.1 `Optimize Game Objects` has a precondition; do not casually turn it off

With it on, **bones are no longer GameObjects** and live inside the Animator instead. So:

- `SkinnedMeshRenderer.bones` becomes an empty array and `rootBone` is null
- Anything that "attaches something to a dancer's hand" stops working

**This project can turn it on because no code reads bones** (`mixamorig` / `.bones` /
`GetBoneTransform` have 0 hits under `Assets/Scripts`). The director binds only the `Animator`, via
Playables; guide orbs and beat targets use the player's hands, not the dancers' bones.

If something ever does need attaching, expose that one bone through the Rig tab's **Extra Transforms
to Expose**, **not by turning the whole switch off** - that would add back 1170 Transforms.

### 12.2 The bone-weight 3 MB was always dead weight

`QualitySettings` currently has a single level, `Mobile`, with `skinWeights = 2`. So **at runtime only
2 bones were ever used**, and the extra 2 sets of weights in the mesh were loaded and then ignored.
This item is therefore 3.04 MB at **zero visual cost**.

> ⚠️ But the **committed** `QualitySettings.asset` also has a second level, `PC`, with
> `skinWeights = 4` (you deleted the PC level locally, and that deletion was not committed). Anyone
> running at the PC level will see skinning drop from 4 bones to 2. This project targets a standalone
> headset, where `Mobile` is the level in effect.

### 12.3 The step not taken: one shared set of dancers

Of the 18 instances only 3 are ever alive (spacing 55.3 m, render radius 20 m, midpoint 27.7 m from
each, so at most one stage is in range). Making the 3 dancers a single scene-wide set, re-parented to
the current stage, would give:

```
18 instances → 3      Animators 18 → 3      and independent of stage count (O(1))
```

**Not done, because v6 already brought Transforms down to 198, and further gains are far smaller than
the cost**: the dancers would become the same kind of thing as the beat/guide gameplay (one scene-wide
set, re-mounted per stage), which conflicts with "every stage has its own dancer avatars". Revisit if
198 Transforms measure as still too many on the headset.

## 14. Auto progression: dissolve, grow, travel (v7)

Script, p. 1: "the timeline will move **automatically and linearly** if the person stays stationary
to watch it as an interactive documentary". v7 builds that, and fills the hole left by removing the
teleport anchors along the way.

### 14.1 The state machine per stage

`TimelineDirector` (on `Play Controller`, with the other always-on components):

| Phase | Duration | What happens |
|---|---|---|
| Dwell | 3 s | On the stage; panel / video / dancers / gameplay run as normal |
| Dissolve | 1.5 s | The dome and floor dissolve together, then the whole thing is `SetActive(false)` |
| Grow | 3 s | The white line grows from this stage to the next |
| Settle | 2 s | A beat's pause, so you can see where the line went |
| Travel | 2×0.35 s | Fade out → move + turn → fade in |

About 9.5 s per stage; measured **60.5 s** to cover all six.

> **`dwellSeconds` is a placeholder.** When it becomes "the immersive video has finished", **only the
> single method `StageComplete()` needs to change** - the rest of the state machine does not need to
> know the difference. This seam is deliberate.

### 14.2 It does not decide "which stage is current"

**It only moves the player.** Standing is still owned by `DancePlaceManager`'s distance test -
travelling to the centre naturally lands within 6 m, and the panel, video and gameplay start by
themselves.

Two components each believing they own "the current stage" is the most common way systems like this
rot, so there is only one authority.

### 14.3 Two things done along the way

**Warm the next stage's video during those 3 seconds of growth** (`WarmVideo()`). Arriving shows a
picture immediately, avoiding the decoder's 1.7-second warm-up black.

**Being carried away commits the score instead of voiding it.** The `Abandon()` that `LeaveStage()`
uses was designed for "the player walked off"; being taken away by the system is a completion. New
`PlayModeController.CompleteStage()` calls `FinishPass()` first and then the normal teardown;
`DanceFollowScore.FinishPass()` was made public for this.

### 14.4 The dissolve shader - **applied to the sphere only**

`Assets/Shaders/DomeDissolve.shader`, **used only by the dome** (`_Cull` 2 / `_Sweep` 0.55 /
`_NoiseScale` 8).

> **The round floor does not dissolve and does not disappear.** It is the ground under the player -
> watching the floor crumble from under your feet in a headset is an entirely different thing from
> watching the room open up overhead, and the latter is far more comfortable. Once the dome has
> dissolved, what remains is a bright disc marking where a stage used to be, with the white line
> running right across it.
>
> So the floor has been **switched back to an opaque `Universal Render Pipeline/Unlit` material**: it
> no longer needs alpha clip, and a surface this large under alpha test is worth avoiding on a
> standalone headset - it would cost early-Z for everything drawn behind it.

**⚠️ When the dissolve completes, it is the sphere's `Renderer` that is switched off, not the `Dome`
GameObject.** The floor is a child of `Dome`, and switching off the GameObject would take the floor,
its `MeshCollider`, and the ground under the player's feet with it.

**The noise is procedural and in object space, with no texture.** Object space because both meshes
are unit-sized, so one `_NoiseScale` reads the same on an 8 m dome and a 3 m floor, and the pattern
does not drift as the object moves. No texture because an equirectangular sphere squeezes a texture
to a point at the poles - which is exactly the most visible spot from inside the dome.

**It samples `_BaseMap`**, even though the dome is currently a flat colour. The sphere's UVs were made
equirectangular for 360 video in the first place, and hooking up immersive video later should not
mean rewriting this shader.

**Alpha test rather than transparency**: the dome is a room, and until it is gone it must keep writing
depth and keep occluding what is behind it.

The threshold overshoots as `_Dissolve × (1 + _EdgeWidth)`, guaranteeing that at `_Dissolve = 1`
**nothing at all is left** (verified by measurement); the edge glow is multiplied by
`step(0.0001, _Dissolve)`, or every dome would wear a glowing rim before anything had started.

### 14.5 ⚠️ The fade cannot be a Canvas

`ScreenFade` is **a world-space quad parented under the camera** (0.1 m out, 0.6 m square, covering
about 143°), not a `Screen Space - Overlay` Canvas.

**An Overlay Canvas does not render in a headset at all** - a fade built on one is perfect on a monitor
and simply absent in the headset. It is the kind of trap nobody notices for two months.

At opacity 0 the whole Renderer is switched off: a full-screen transparent quad over both eyes is not
free on a standalone headset.

### 14.6 A trap: detecting a moment with a frame-wide window

The first version detected "just crossed the fade-out midpoint" inside `Travel()` with
`elapsed < half + Time.deltaTime`. **That is a window one frame wide, and depending on where the frame
times land it can fire zero times or twice.**

Firing twice incremented `index` twice, **skipping an entire stage's Dwell and Dissolve**. Measured
symptom: after a full run, Stage 3's `dissolve` was still 0 and its dome still standing, while the
other five were fine - and the player did reach the end, so from the outside everything looked
"roughly right".

Changed to an explicit `arrived` flag. **Any "this frame just crossed some moment" test should use a
flag, not a `Time.deltaTime` window.**

### 14.7 Tunable variables

| Variable | Location | Default |
|---|---|---|
| `dwellSeconds` | `TimelineDirector` | 3 s (placeholder, see §14.1) |
| `dissolveSeconds` | `TimelineDirector` | 1.5 s |
| `growSeconds` | `TimelineDirector` | 3 s (**a fixed time per leg**, not a speed) |
| `settleSeconds` | `TimelineDirector` | 2 s |
| `fadeSeconds` | `TimelineDirector` | 0.35 s (each way) |
| `matchFacing` | `TimelineDirector` | true |
| `_EdgeColor` / `_EdgeWidth` | both dome materials | warm orange / 0.08 |

### 14.8 Known problems

**14.8a The line may grow too fast.** Each leg's actual path is about 78.4 m; 3 seconds = **26 m/s**.
Not yet seen in a headset.

**14.8b It stops after stage 6.** No loop, no return; the player is left standing at the end of the
line.

**14.8c None of it has been verified in a headset** - especially the comfort of being turned
passively plus the fade, which cannot be judged while developing seated.

## 15. The prologue (v8)

Before the timeline appears the script has another passage: the player is born into an all-black
space, a narrator tells the story of IFEL, objects appear around you one by one as she names them,
and then "the objects around us slowly slide away into the distance but the timeline on the ground
counts back to 1964" (p. 2).

**The prologue is not a stage.** It happens off the line, at its own anchor (world position
`(-45, 0.1, -20)`, 49 m from Stage 1, well beyond that stage's 20 m dancer radius, so no dome lights
up behind you), with the white line at zero length throughout.

### 15.1 Components

| | |
|---|---|
| `PrologueCue` | One beat: speaker, subtitle, **voice clip**, duration, which props appear on it, whether everything is dismissed |
| `PrologueDirector` (on `Play Controller`) | Walks the cue list, then hands over to `TimelineDirector` |
| `SubtitleDisplay` | World-space subtitle panel that **lazily follows** the head |
| `PropReveal` | One prop's appear / linger / dismiss |

### 15.2 Voice: recorded, and recorded only once (v9)

**Every cue's `voice` has a clip assigned.** They were generated from the subtitle text through
ElevenLabs by the editor menu `RH Community Hack/Narration/Generate Missing Voice Clips`
(`Assets/Scripts/Editor/NarrationBaker.cs`), stored as `Assets/Audio/Narration/NN-label.mp3`, with a
`.txt` beside each recording the line it was made from.

The tool **never pays twice**:

```
cue already has a clip   →  untouched (only warns if the subtitle has changed: "audio and subtitle no longer match")
mp3 already on disk      →  assigned directly, no request
neither                  →  one request, write the file, assign it
```

To re-record a line: delete its `.mp3` and run the menu again. `Assign Existing Clips Only` is the
offline version, for a clone that has the clips but not the key.

Advancement is still concentrated in the single method `PrologueDirector.CueComplete`:

```
clip assigned  →  the beat lasts until the audio finishes, plus holdAfter (counted from the END of the audio)
no clip        →  the beat lasts `seconds`
```

**The API key is not in the repository and must never be.** `NarrationBaker` reads the environment
variable `ELEVENLABS_API_KEY`, or else `.ai/secrets/elevenlabs.key` (gitignored). The key is
restricted (text-to-speech only, no `voices_read`), so the voice is the constant
`XrExE9yKIg1WjnnlVkGX` ("Matilda", a warm American female voice). The script's narrator is an old
Black woman, and nothing in the default voice library fits; changing the voice = change the constant
+ delete the seven files.

The `AudioSource` (`Play Controller/Narration`, 2D, not spatialised) is unchanged.

### 15.3 Hand-over: `index = -1` is deliberate

When the prologue ends it calls `TimelineDirector.Begin()`, which sets `index` to **-1** and enters
the `Grow` phase. The **existing loop, untouched**, then performs the opening:

```
Grow    the line grows from 0 to the first stage   ← the script's "a zigzagging timeline ... that starts underneath our feet"
Settle  a beat's pause
Travel  fade out → move the player onto it → fade in
Dwell   the normal loop begins
```

There is no dedicated opening path to keep in step with the main loop. `LengthAtStop(-1)` returning 0
exists for exactly this.

> `TimelineDirector.autoStart` must be **off**, or it and the prologue will drive the player at the
> same time. An `Idle` state and `IsWaitingToBegin` were added.

### 15.4 ⚠️ The subtitle panel follows lazily - by design, not by laziness

Text welded to the head is one of the most reliable ways to make someone sick - it never stops, and
the eyes can never settle on it. So the panel **stays put while you turn your head**, and only starts
following once your gaze has moved more than `deadZoneDegrees` (18°) away, smoothly rather than
snapping. During the prologue the player is meant to be turning to look at objects appearing around
them, and this is what decides whether the subtitles can be read at all.

Likewise it **cannot be a `Screen Space - Overlay` Canvas** - that does not render in a headset at
all, the same trap as `ScreenFade`.

### 15.5 Props appear by scale, not by fade

A fade requires every material on the object to be transparent, and these FBXs carry up to 25
materials each. Scale works on any material.

**The 8 rigged props (4 glasses, 4 cameras, v10) really dance**: each FBX carries a 20-frame
`preset:biped:freaky` take, imported as the looping clip `dance`, and each scene instance has an
Animator (`Assets/3DModel/Animation/<name>.controller`, one state, no root motion). These 8 **no longer
spin**; instead `PropReveal.faceViewer` yaws them toward `Camera.main` only, following smoothly at
90°/s and snapping square on the first frame of the reveal - a performer dances to someone, and a
spinning one would have its back to the audience half the time. The models' front is +Z, so
`facingOffsetDegrees` = 0. The unrigged props (the 5G tower and the three soft-technology models) keep
the original bob + spin.

`PropReveal` keeps the whole GameObject **switched off** until it appears - which matters more than
the animation itself: the prologue props add up to millions of triangles, and those whose turn has not
come should cost nothing. While lingering they bob gently and turn slowly, because the script insists
these things are **dancing** ("vertical and dance to the music", "they also dance to the music",
"appear and start dancing").

### 15.6 The prologue's seven beats (subtitles are pure English, verbatim from the script PDF pp. 1–2)

| # | cue | Subtitle (script's own English) | Appears | Audio |
|---|---|---|---|---|
| 0 | ifel intro | The Immersive Festival Live project, IFEL, presents a new way for festivals, concerts, live TV shows, parties, and other productions to be shared around the world. | — | 11.3s |
| 1 | ar glasses | New technology like Augmented Reality glasses, | `specs` `metaquest` `androidxr` `orion` | 3.1s |
| 2 | immersive cameras | spatial audio, immersive cameras, | `blackmagic` `cannon` `insta360` `gopro` | 2.6s |
| 3 | low latency | and low latency streaming give us the ability to bring people together like never before. | `5gtower` | 5.6s |
| 4 | soft technologies | But this project isn't just about the technology coming together. At its core, it was always about bringing people together. It was about co-creation. It was about healing. It was about Waging Love. | `unity` `cocreation` `healing` | 13.5s |
| 5 | where it started | And to understand how these technologies come together, you have to know where the project started. | — | 5.4s |
| 6 | count back | And it started with a vision of the future. That vision grew into a dream. | all dismissed | 4.5s |

The script's second narrator block is split into beats 1–3 so that the glasses, cameras and tower
appear **at the moment each is named**; each beat was generated with its neighbouring lines as
`previous_text` / `next_text`, so "Augmented Reality glasses," is not read with a full stop after it.
The third block is split at "future" so the dismissal lands where the script puts it.

Measured: beats advance on audio length, hand-over at t≈55s, the timeline starts running with every
prop dismissed, console clean.

## 16. Era props (v8)

The stage prefab gained an empty `Props` mount, which `DancePlace.props` points at. **Instances parent
their own props under it** - so they are added GameObjects rather than component overrides, and
`take` remains the only component override.

`Props` is switched by **the same distance radius** as the dancers (20 m + 3 m hysteresis). One of these
models alone is six figures of triangles; they cannot stay loaded throughout.

| Prop | Stage | Script basis |
|---|---|---|
| `hitsville` | Stage 2 - 1964 | "a 3D Model of HITSVILLE USA/MOTOWN" (p. 2) |
| `CRTV` | Stage 3 - 1984 | "a GIANT TV appears around the video" (p. 3) |
| `minicity` | Stage 3 - 1984 | "as she says the different cities, a mini city model" (p. 4) |
| `tech-totem` | Stage 4 - 2016 | The music-technology montage between 1984 and 2016 (p. 5), placed at the era it leads to |

### 16.1 Two placement traps

**One: a world-space pivot offset cannot be added straight to `localPosition`.** The first version did
exactly that, and since the prologue root and every stage are rotated, the 5G tower ended up above the
player's head. The correct approach stays in the parent's own space throughout:
`parent.InverseTransformVector(wantedWorld - bounds.center)`.

**Two: do not assume a model's longest axis is Y.** `tech-totem`'s mesh has a local extent of
`0.003 × 0.003 × 0.010` - it is **lying down**. Normalising by height stretched it into a 12.5-metre
log. Normalise by the longest dimension instead.

> The TV was also moved back: it is a chunky 6×4.2×5.9 body, and its original position swallowed the
> video screen at z=2.5 entirely. Its near face is now at z=2.8, framing the screen rather than
> covering it.

## 13. Explicitly out of scope

- **Per-stage gameplay parameters** (different BPM, different judgment tolerance). All stages currently
  share one configuration.
- **Saved scores / accumulating across stages.** Leaving clears everything; nothing is kept.
- **Song and mode selection on the panel** (see 11.3).

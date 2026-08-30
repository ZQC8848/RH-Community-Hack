# Beat positions come from a recorded take, sampled at the moment the beat is HIT

**2026-08-29 ・ status: standing ・ scope: where beats appear. Not when they appear - the cadence is still a timer - and not the judgment loop itself**

## What forced the choice

Beats appeared at random points inside a scatter volume. That was always scaffolding; the
design calls for them to come from a real performance, and `BeatSpawner`'s own comment had
said so since it was written. With three recorded takes in hand, positions could stop being
arbitrary.

## Decision

**Positions come from a `DanceRecording`; the cadence stays a fixed timer.** Each tick samples
the take and places one beat per hand.

Three things this rests on, each of which had an obvious wrong version:

1. **Sample at `T_perfect`, not at spawn time.** A beat is telegraphed for its config's
   `ringLeadTime` (1.2s) before it can be hit, so the source is asked where the hand should be
   *at the hit moment*. `BeatPlacementSource.GetPlacements(double perfectTimeDsp, ...)` is named
   for it and says so in the comment.
2. **The hand chooses the flavour.** `BeatConfig_Cyan.allowedHands = Right` and
   `Magenta = Left` already existed, so a placement that names its hand determines its colour.
   The old random flavour pick was deleted.
3. **The take is reached through an abstraction.** `BeatSpawner` lives in `Interaction/`, which
   by standing decision must not depend on the capture system, so `DanceRecordingBeatSource`
   sits in `DanceCapture/` behind `BeatPlacementSource`. Same split as
   GuideOrb / DanceFollowScore - see [modular-portable-interaction.md](modular-portable-interaction.md).

Anchoring: `DanceReferenceFrame.Capture(head)`, taken on the **first placement** rather than in
`Start()` - an XR head pose is not valid until the rig has had a frame, so anchoring in `Start()`
pins the whole chart to the camera's placeholder pose.

## Why not the alternatives

| Alternative | Its case | Why not |
|---|---|---|
| **Sample at spawn time** | The obvious reading of "put a beat where the hand is" | Offsets the entire chart 1.2s ahead of the dance it came from. Nothing errors, the beats move, the data is real - it simply lines up with nothing. This is the failure mode this record exists to prevent |
| Let `BeatSpawner` reference `DanceRecording` directly | One fewer type; no abstraction to explain | Welds the portable interaction module to this project's recording format. The module is meant to move to a multiplayer project later |
| An `interface` instead of an abstract MonoBehaviour | Cleaner in C# terms | Unity will not serialise an interface as a component reference in the inspector, so the scene could not wire it |
| Anchor to the scene's Anchor marker instead of the head | Designer-controlled, predictable, testable without a headset | Recorded positions are relative to the dancer's *head*. Anchoring elsewhere throws away the thing that makes the data mean something, and does not follow the player |
| Derive the **rhythm** from the take too | The actual design goal | Beat detection from a trajectory is unbuilt and unprototyped - the biggest open unknown in the design. Positions were the half that could be done now |

## What this rests on

| Assumption | Status | Evidence, or the check that would settle it |
|---|---|---|
| **Raw motion data makes a playable chart** | **measured, and the answer is "not entirely"** | Hand-to-head distance across one take runs **0.18m to 1.09m**, against an arm reach of roughly 0.3-0.8m. Some beats land within 20cm of the face; some are out of reach without stepping. No distance filter yet - a deliberate choice, revisit after a headset session |
| One lead time serves every flavour in a tick | verified for now, guarded | The source is asked about a single moment, so the whole tick shares one lead. Both configs are 1.2s; `BeatSpawner` warns once if flavours ever disagree |
| Anchoring on first placement is late enough for a valid head pose | unverified | Holds in the editor. Check in a headset that beats do not land relative to a stale pose on the very first tick |
| Beats at 2 per tick are the right density | unverified | 1.5s interval over a 48.7s take gives 64 beats per loop. Feel-test it |

## Accepted costs

The cadence is still a metronome, so the chart follows the dancer in space but not in time - it
will not feel musical until rhythm extraction exists. Positions being right is a prerequisite
for that work, not a substitute for it.

## What would reverse this

- Beat detection from the take lands → the timer goes, and this source grows a time dimension
- Playtesting shows raw positions are unusable without filtering → the filter becomes part of
  the source rather than an optional extra

---

## When this is superseded

*(not yet superseded)*

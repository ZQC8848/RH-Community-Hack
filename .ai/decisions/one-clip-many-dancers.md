# On-stage dancers are driven by a Playables graph, one playable each, sharing a time value

**2026-08-29 ・ status: standing ・ scope: how the character models are animated. Not where they stand, and not the controller-pose data that drives the orbs and the beats**

## What forced the choice

A take now carries a skeletal version of the same performance
(`DanceRecording.characterAnimation`), and the scene should show it being danced by more than
one character at a time. Three instances of the same model, one clip, in unison.

## Decision

**A `PlayableGraph` with one `AnimationClipPlayable` per dancer, all given the same time value
each frame.** `DanceCharacterDirector` owns the graph; the clip arrives from the take, pushed in
by `PlayModeController` like every other take-derived thing.

No `AnimatorController` is involved. The models import with a controller-less `Animator`, and the
graph plays the clip straight into it - which is why adding a fourth dancer is one more array
element rather than another state machine.

### The tidier version does not work, and fails silently

The obvious design is **one** clip playable with three `AnimationPlayableOutput`s all pointing at
it. That would make synchronisation structural: one clock, one evaluation, nothing to keep in
step.

**Only the first output is driven.** The other two dancers stand in bind pose, and nothing is
logged anywhere - no error, no warning. Measured directly:

```
first attempt, one shared AnimationClipPlayable:
  Dancer Left   LeftForeArm=(9.35, 9.62, 259.27)   Hips.y=0.965
  Dancer Right  LeftForeArm=(0.00, 0.00, 0.00)     Hips.y=0.940   <- bind pose
  Dancer Lead   LeftForeArm=(0.00, 0.00, 0.00)     Hips.y=0.940   <- bind pose
  => 1/3

after: one playable each, one shared time value
  all three  LeftForeArm=(12.08, 2.87, 240.47)     Hips.y=0.9703
  => 3/3, bit-identical
```

So the playables are duplicated and the **time** is what is shared:

```csharp
for (int i = 0; i < clipPlayables.Length; i++) clipPlayables[i].SetTime(t);
graph.Evaluate();
```

Sync is still a property of there being one clock. It is just pushed rather than structural.

`graph.SetTimeUpdateMode(DirectorUpdateMode.Manual)` for the same reason: if the graph advanced
its own clock as well, two things would be deciding where we are.

## Why not the alternatives

| Alternative | Its case | Why not |
|---|---|---|
| **One shared clip playable, three outputs** | Sync guaranteed by construction | Drives only the first output, silently. This is the record's main finding |
| An `AnimatorController` with the clip as its default state | No Playables API to learn; visible in the inspector | An asset to author and keep in step with the take, per clip. The clip is chosen at runtime from the take, so a controller would have to be rebuilt or overridden anyway |
| Three `Animation` components (legacy) | Simplest possible | Legacy pipeline; the clip imports as non-legacy and would need converting, and it does not solve syncing either |
| Let each dancer own its own director | No array to wire | Three clocks. They start together and drift apart, which is the exact failure the shared time value exists to prevent |
| Give the director its own clip field | Fewer hops than pushing from the controller | A second place a clip can be chosen. PlayScene has just been through removing exactly that - see [play-scene-two-modes.md](play-scene-two-modes.md) |

## What this rests on

| Assumption | Status | Evidence, or the check that would settle it |
|---|---|---|
| All dancers share the rig the clip was authored against | **holds by construction** | They are instances of the same FBX. A different model would need its own clip or retargeting, and the rig is `Generic` with no Avatar, so retargeting is not available |
| One playable each is affordable | verified for 3 | Each is a full clip evaluation. Three is nothing; thirty on a Quest is an open question |
| The graph is destroyed on teardown | verified in code, not under stress | `PlayableGraph` is not garbage collected. `OnDisable`/`OnDestroy` destroy it. A leak would keep writing to the Animators it was bound to |
| Dancers need not line up with the take | **assumed, and currently false-by-design** | Clip is 29.93s, take is 48.67s. The director loops on its own clock - measured take at 11.95s while dancers were at 18.34s. Nobody has said whether they should align |

## Accepted costs

The dancers perform on their own timeline. They are set dressing that happens to be the same
performance, not a synchronised reference the player can dance against. If they are ever meant to
be the latter, this needs a decision about *how* - trimming the clip, retiming it, or driving the
director from `DancePlayer.PlayheadSeconds` - and none of those is free.

## What would reverse this

- The dancers must line up with the take → the director stops owning a clock and follows the
  player's playhead instead
- Dancer count grows enough that per-dancer clip evaluation costs real frame time → look at
  animation instancing rather than a graph per character

---

## When this is superseded

*(not yet superseded)*

# A dance take's player frame is snapshotted once and frozen, not tracked continuously

**2026-08-26 ・ status: standing ・ scope: covers how recorded poses are made player-relative; not beat extraction, not how takes become playable levels**

## What forced the choice

Recorded controller motion has to be replayable for a player standing somewhere else,
facing some other direction, so it cannot be stored in world space. That means picking a
"player space" to store it in — and the obvious choice, a frame that follows the player,
turns out to have a problem: a dancer glancing sideways mid-take would rotate the
coordinate system out from under their own recording, smearing head-look into the
choreography.

## Decision

Capture the reference frame **once**, then freeze it. Origin is the head position
(including height); orientation is head-forward projected onto the horizontal plane, yaw
only. Head rotation is not recorded at all.

- **Recording** snapshots the frame when the take truly starts — after the 3s countdown, by
  which point the dancer has settled into position — not when the button was pressed.
- **Playback** calibrates on the first Play and then **keeps that origin across loops**, so
  every pass replays in exactly the same place and successive passes are directly
  comparable. Re-anchoring is an explicit, deliberate act: **hold B for 3 seconds**.

*(Amended 2026-08-26: playback originally re-captured the frame on every loop so the preview
would follow a viewer who wandered off. That was replaced with the hold-B model at the
user's request — a preview that quietly moves between loops makes it impossible to tell
whether a change came from the take or from the anchor drifting.)*

## Why not the alternatives

| Alternative | Its case | Why not |
|---|---|---|
| Frame follows the head continuously (full 6DoF) | Truly "relative to the player" at every instant | Turning the head swings the whole recorded frame; hands would appear to move when only the head did |
| Frame follows head position + yaw, continuously | The standard "body space"; handles a dancer who walks around mid-take | Head yaw ≠ body yaw. A glance sideways rotates the frame, so it needs low-pass filtering with a time constant nobody can guess without data — an extra tunable that the frozen frame removes entirely |
| Floor-projected origin (Y = height above floor) instead of head-height origin | "Reach this high" is absolute and easy to reason about | "This far from my head" transfers between dancers of different heights better, which is the harder problem (see the height-normalisation open question in the design doc) |
| Store raw world-space poses, derive player-space at read time | The frame definition stays revisable without re-recording | Genuinely attractive, and was the initial proposal — but a frozen frame makes the definition so simple that there is little left to revise, and storing relative keeps the asset directly inspectable |

## What this rests on

| Assumption | Status | Evidence, or the check that would settle it |
|---|---|---|
| A dancer does not turn their body far enough during one take for a frame frozen at t=0 to become wrong | unverified | Record a real take with deliberate turning and see whether the replayed motion still reads correctly. If a 180° turn mid-dance breaks it, the fix is per-section takes rather than reintroducing continuous tracking |
| Head-height origin transfers across dancers better than floor-height | unverified | Needs two dancers of noticeably different height performing the same take |

## Accepted costs

A take is anchored to wherever the dancer was standing when they hit record. Choreography
that involves travelling a long way across the room is stored as large offsets from that
one point rather than as motion relative to a moving body — fine for arm-level dance, and
`headPosition` is recorded precisely so it is visible when a take travels too far.

## What would reverse this

- A real take shows visible distortion because the dancer turned their body substantially mid-recording
- Beat extraction turns out to need continuous body orientation (e.g. to express a beat as "to my left" at a moment when the dancer has turned)

---

## When this is superseded

*(not yet superseded)*

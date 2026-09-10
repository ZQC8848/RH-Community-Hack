---
base: "[[Ideas.base]]"
Type: "Idea"
tags:
  - VR
  - rhythm-game
  - hackathon
  - interaction-design
status: concept stage
date: 2026-08-18
---
## Project

RH Community Hack

## 1. In one sentence

A VR rhythm dance game: the player holds a controller and hits spheres that appear in the space
around them in time with the music. Each sphere's spawn time and 3D position come straight from
the controller trajectory recorded off a real person dancing - no hand-authored charts.

---

## 2. The core interaction

### 2.1 A single beat

- Every beat is one **sphere**: the player puts a VR controller inside it and the beat is done.
- Around the sphere sits a larger **ring** (the cue ring), which shrinks toward the sphere over
  time as an advance visual cue for the rhythm - like Beat Saber's incoming blocks, but with a
  concentric ring closing in instead of an object flying at you.
- Judgment has three grades (Perfect / Good / timed-out miss). The ring renders as a billboard,
  and the art direction is "neon energy pulse" (Fresnel rim glow + emission, with judgment grade
  distinguished by colour). The judgment state machine, the windows, and the designer-tunable
  variable table are implementation detail and live in
  `Docs/Ring-Sphere Judgment and Art Spec.md` in this repo - as of 2026-08-20 that file is the
  implementation spec for this part, and this document keeps only the high-level description.

### 2.2 Chart generation: mapping a real performance (the core insight)

> Progress (2026-08-26): recording and playback are implemented, see
> `Docs/Dance Capture Recording and Playback Spec.md` in this repo. **Beat extraction is not
> implemented** - trajectories can be recorded and played back for observation, but no chart is
> generated from them yet.

Rather than placing beats by hand:

1. Put a real person in VR and let them dance / keep time freely to a song, recording the full
   6DoF controller trajectory (position + timestamp) aligned to the music's timeline.
2. Later, to map it into a chart, all that is needed is:
   - Pick or detect the beat moments in the recorded trajectory (from the music's beat onsets, or
     from sudden changes in controller speed / points where the hands pause) → this gives each
     sphere's **spawn time**.
   - The recorded controller position at that moment → used directly as that sphere's **position
     in space**, with nothing further to design.
3. In other words, "record someone dancing" solves the two hardest problems in rhythm-game level
   design at once - **when the beats fall** and **how the player moves through space** - turning
   chart authoring into the problem of "find someone who dances well to this song".

### 2.3 Why this could work

- The bar for level design drops from "chart designer + 3D spatial choreography" to "find someone
  with a good sense of rhythm and record one pass".
- The resulting hit points naturally sit inside human reach and follow natural dance lines; none
  of the physically awkward positions that procedurally generated charts tend to produce.
- Swapping the performer swaps the level's style and difficulty, which in principle makes a
  low-cost UGC chart pipeline possible.

---

## 3. Open questions / next steps

- [ ] Automatic beat detection: music onset detection vs. controller motion features (speed peaks,
      sudden acceleration, pauses), or a weighted mix of both
- [ ] How to calibrate the ring's lead time so the cue reads consistently across different BPMs
- [ ] Whether reusing the performer's positions directly needs spatial normalisation (players of
      different height / arm span vs. the performer)
- [ ] Hit tolerance (sphere radius; how a controller counts as "inside" - centre point vs. collider)
- [ ] How to handle two hands / several simultaneous spheres on the same beat

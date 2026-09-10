---
status: v1 (implemented 2026-08-26)
date: 2026-08-26
related: "Idea - VR Rhythm Game Interaction Paradigm.md" (same folder, high-level concept)
---

# Dance Capture Recording and Playback Spec

The first stage of implementing the core mechanic from section 2.2 of the design doc - "put a real
person in VR, record the controller trajectory, and use it to generate a chart": **record the
trajectory, save it as an asset, and play it back to watch**.

**Explicitly out of scope**: extracting beats automatically from a trajectory (the biggest open
problem in the design doc). This system only produces a clean time series; beat extraction is its
**downstream consumer**.

## 1. Reference frame: snapshot once, then freeze

This is the most important design decision in the whole system.

- **At the moment recording actually starts** (when the countdown ends, not when X was pressed),
  the player's reference frame is snapshotted once: origin = head position, forward = the head's
  forward projected onto the horizontal plane (yaw only). From then on it is **frozen** and no
  longer follows the player moving or turning their head.
- **Playback calibrates only once, on the first play**, and **does not re-sample on loop**, so
  every pass lands in exactly the same place and passes can be compared side by side.
- To re-anchor: **hold B for 3 seconds** (right controller `secondaryButton`; the B key works too).
  After recalibration playback restarts from the beginning rather than jumping mid-phrase. The UI
  shows a hold progress bar.

**Why frozen rather than continuously following**: if the reference frame kept following the head,
then glancing sideways while dancing would rotate the entire coordinate system and contaminate the
recorded motion. Once it is frozen, head rotation becomes a non-problem entirely - no low-pass
filtering or body-orientation estimation needed.

**The origin is the head position including height, not projected to the floor**: this makes a
recording mean "how far from my head" rather than "how high off the ground", which transfers
between people of different heights noticeably better.

**Implementation**: `DanceReferenceFrame` (`Assets/Scripts/DanceCapture/`). It handles the
degenerate case where the head points straight up or down and the horizontal projection collapses.

## 2. Data model

```
DanceRecording (ScriptableObject)
├─ label / capturedDuration / averageSampleRate
├─ samples[]: { time, headPosition, leftPosition, leftRotation, rightPosition, rightRotation }
└─ inPoint / outPoint      ← trimming, non-destructive
```

- **Head rotation is not recorded**: the reference frame already fixed the body's orientation at
  the start, and where the dancer happens to look is not part of the choreography.
- **Head position is recorded**: to know how far the dancer travelled, and so judge whether a
  routine fits a given play space.
- **Timestamps come from `AudioSettings.dspTime`**, the same clock the judgment system uses. The
  whole mechanic depends on the trajectory lining up with the music, and `Time.time` drifts.
- **Sample rate cap `maxSampleRate` (default 90Hz)**: the actual rate is
  `min(frame rate, maxSampleRate)`. **The cap is necessary** - unlocked in the editor this ran at
  800+ Hz, and a 15-second recording measured 2.9MB; with the cap, 14 seconds is 155KB. Headsets
  themselves only run at 72-120Hz, so anything beyond that is waste.

## 3. Trimming: non-destructive

`inPoint` / `outPoint` only **narrow the playback range; they never delete sample frames**.

- They can be changed back at any time, and several different clips can be trimmed out of one
  recording
- `outPoint <= 0` means "play to the end"
- **Do not "apply" a trim by deleting samples**

Confirmed as a requirement: **trimming only, no time-stretching**.

## 4. Recording flow

| Action | Trigger |
|---|---|
| Start recording (enters a 3-second countdown) | Left controller **X** (`<XRController>{LeftHand}/primaryButton`); keyboard **X** also works |
| Cancel the countdown | Press **X** again during the countdown (treated as a mis-press, not an early start) |
| Stop and save | Press **X** while recording |
| Recalibrate the playback origin | **Hold B for 3 seconds** (right controller `secondaryButton` / keyboard B) |

**State machine**: `Idle → (when a video is attached) PreparingVideo → CountingDown (3s) →
Recording → Idle`. **Nothing is sampled during the countdown**; the timeline starts the instant the
countdown ends (measured first frame at t≈0.008s).

- The bindings are **standalone InputActions created in code**, not written into the shared XRI
  action asset - a development tool should not be able to disturb the input maps the game itself
  depends on.
- ⚠️ **Keyboard X is a convenience binding for desktop debugging and is easy to hit by accident in
  the editor**: with the Game view focused, any stray `x` starts a recording. During development
  this produced an accidental 7-second recording. The headset does not have this problem (there it
  is the controller's X button). If it becomes annoying, drop the `<Keyboard>/x` binding or make it
  a chord.
- On stop, the take is saved automatically as an asset under `Assets/DanceRecordings/`, **with a
  timestamped filename**: `Dance_2026-08-26_16-20-40.asset`. Repeated recordings therefore never
  overwrite each other, and the filename itself says when it was recorded.
  `GenerateUniqueAssetPath` is the fallback for the extreme case of two recordings finishing within
  the same second.
- Starting a recording automatically calls `Stop()` on the player (the `playerToStop` reference),
  so a looping preview's music does not play over the music being recorded.
- The UI is all English, on a world-space canvas below the head, always in view:
  - Idle: `● RECORD MODE` + `Press X to start recording` + (when music/video is attached)
    `(video + music will play)` and similar hints
  - Buffering video: `BUFFERING VIDEO...` + `The countdown starts once the video is ready` (yellow)
  - Playback mode: `▶ PLAY MODE` + the name and progress of what is playing +
    `Clear the Recording field on Dance Player to record again`, with **a footer line separated by
    a rule at the bottom of the panel**: `Hold B for 3s to recalibrate origin` (blue)
  - The hold-B hint **only appears in playback mode**: recalibration re-anchors the **playback
    origin**, whereas the recorder snapshots its own reference frame the moment recording starts,
    so in record mode that line would point at something that does not exist
  - Countdown: `GET READY` + large seconds + `Press X to cancel` (yellow)
  - Recording: `● RECORDING 0:12.4` + sample count + `Press X to stop and save` (red)
  - After saving: `SAVED <name>` + duration / sample rate / music name, returning to idle after a
    few seconds
  - While holding B: `RECALIBRATING ORIGIN` + seconds remaining + progress bar (green)

## 4a. Two modes, decided by "does the player have data attached"

`DanceCaptureModeController` distinguishes the modes with one rule:

| DancePlayer's Recording field | Mode | Behaviour |
|---|---|---|
| **empty** | `● RECORD MODE` | The recorder is enabled, X works |
| **has data** | `▶ PLAY MODE` | **The recorder is disabled entirely**, and that data starts playing |

- To get back to record mode: **clear the Recording field on DancePlayer**. The UI says so
  literally.
- Assigning or clearing it in the Inspector at runtime takes effect immediately; there is no need
  to leave play mode.
- **Why this logic does not live on DanceRecorder itself**: a component that disabled itself no
  longer runs `Update()`, so it would never get the chance to re-enable itself when the data is
  cleared. The mode switch therefore has to be held by a component that keeps running in both
  modes.
- `DanceRecorder.StartCountdown()` has a second guard: it refuses outright, with a message, while
  the component is disabled. Otherwise external code calling `Toggle()` would leave it stuck in
  `CountingDown` with `Update()` not running, and switching back would suddenly fire a stale
  recording.

## 4b. Music (optional)

With audio assigned to `DanceRecorder.musicClip`:

1. On X, the audio is scheduled with **`PlayScheduled(dspTime at the end of the countdown)`**,
   **aligned to the recording timeline on the same clock** - measured drift is **1.3e-05 s
   (13 microseconds)**. Using `Play()` on some frame instead would give every recording an unknown
   offset.
2. The audio reference is written into the saved `DanceRecording` asset.
3. Playback plays that audio automatically, offsetting the start by `inPoint`, so sound and motion
   stay in sync after trimming.
4. Leave it empty for no audio; recording and playback are then silent and nothing else changes.

Recording and playback use **separate AudioSources** (`Recording Music` / `Playback Music` in the
scene), so the two never fight over the same clip and playhead.

## 4c. Video (optional)

The scene has a `Video Screen` (a world-space 16:9 quad at `(0, 1.6, 2.5)` facing the rig) with a
`VideoPlayer` rendering into `Assets/DanceCapture/VideoRenderTexture.renderTexture`; the quad
samples that RT through an Unlit material. It is **not** parented under the head - it is a screen
in the room, and does not follow the gaze.

Usage mirrors music: drag a `VideoClip` onto `DanceRecorder`'s `Video (Optional) → Video Clip`; it
plays while recording, the reference is written into the recording asset, and playback replays it
automatically.

- **Audio/video sync is the `VideoPlayer`'s own job**: `audioOutputMode = Direct`, with no extra
  AudioSource, which avoids the whole class of "make an AudioSource keep up with the decoder"
  problems.
- ⚠️ **Attaching both music and a video with sound plays both at once**, two tracks stacked. Use
  one or the other, or use a silent video.

### The video's lifecycle: `DanceVideoScreen` and "never Stop"

`VideoPlayer` **has no `PlayScheduled`**, so it cannot be scheduled precisely against the dsp clock
the way an `AudioSource` can; and while unbuffered, `Play()` silently waits on the decoder. Worse,
as measured on this development machine:

```
empty scene, 1920x1080 H.264, playOnAwake, no project scripts at all
t+0.0s  ~ t+18.0s   time=0.00  frame=-1   (isPrepared had long since been true)
t+18.08s            *** first frame delivered ***
t+20→28s            time 1.80 → 3.80 → 5.80 → 7.80 → 9.80   exactly real-time, zero drift
```

**The decoder takes ~18 seconds to start, and then plays precisely in real time.** That one fact
explains every earlier "only the first frame, and no sound" symptom: the old recording and playback
code called `Stop()` + `Prepare()` on every pass, and a 12.5-second take looping once knocked the
decoder over and started again - the 18-second startup never finished, so the picture stayed on
frame one forever.

The `VideoPlayer`'s lifecycle is therefore now owned by a separate component, `DanceVideoScreen`,
which has one iron rule:

> **Never call `VideoPlayer.Stop()`.** Stop discards the buffered decoder state, which means paying
> the 18 seconds again. Anywhere a "stop" is needed, use `Pause()` + seek back to the start
> (`Park()`).

Its interface is small:

| Method | Purpose |
|---|---|
| `WarmUp(clip)` | Load and start buffering; pause on the first delivered frame and hold at 0, ready to start instantly |
| `IsReadyFor(clip)` | **Readiness is `frame >= 0` (a picture really was delivered), not `isPrepared`** |
| `CueTo(t)` | Seek to a time and stay paused, confirming arrival with `seekCompleted` |
| `Resume()` | Play from the current position |
| `Park()` | Pause + return to 0 |

`DanceVideoScreen.For(videoPlayer)` attaches itself to the `VideoPlayer`'s GameObject when needed,
so **nothing has to be wired by hand in the scene**.

Three timing changes follow from this:

1. **Warm up as soon as the scene loads** (`Warm Up Video On Load`, on by default). Those 18 seconds
   are spent while the headset is being put on and the player finds their spot, so by the time X is
   actually pressed it is usually ready.
2. **`PreparingVideo` waits on `IsReadyFor`**, not on `isPrepared` - the latter goes true a full 18
   seconds before the first frame, and using it as the starting gun was the direct cause of the
   picture stuck on frame one. The UI's `BUFFERING VIDEO...` now carries a seconds counter, so a
   long buffer does not look like a hang.
3. **Looping no longer rebuilds the decoder.** `DancePlayer.Restart Video Each Loop` (on by
   default) seeks back to the in-point at the start of each pass; if the take is much shorter than
   the video and frequent seeking costs too much, turn it off and let the video play through.

> **⚠️ Video-to-motion sync accuracy has not been verified on real hardware.** The music path
> measures only 13 microseconds of drift (because `PlayScheduled` runs on the dsp clock), but the
> `VideoPlayer`'s clock follows the **render loop**, which is not the same thing as the dsp clock.
> Put the headset on, record a take with video, and confirm the motion and the picture line up; if
> there is a consistent offset, a video start offset could be recorded in the asset to compensate.

> **⚠️ The 18 seconds is a measurement from this machine, not a universal value.** Other machines
> may be far quicker. When investigating, run `Assets/Scenes/MinimalVideoTest.unity` first (empty
> scene + one VideoPlayer) - it exists as the control experiment.

### ✅ 2026-08-29: the long startup is solved - transcode to VP8 on import

The "18 seconds" was never a fixed number: **55.65 seconds** was measured on the same machine
later. What is actually slow is **Unity going through the OS's H.264 decoder** (Media Foundation on
Windows).

**Changing the import setting to transcode to VP8 took the first frame from 55.65 s to 1.76 s.**
After transcoding Unity uses its own decoder and bypasses the OS path entirely.

> **Every video added from now on must be set this way.** Select the `.mp4` → Inspector → tick
> **Transcode** → set `Codec` to **VP8** → Apply. The setting lives in the `.meta` and travels with
> the asset, so collaborators do not need to know about it. The first import spends an extra half
> minute transcoding - a one-time cost.

Two other things that were unclear at the time and are now settled:

- **There is no renderer problem.** Reading the RenderTexture back into a Texture2D and averaging
  its brightness showed the brightness changing as soon as decoding began - the RT → material →
  quad chain was fine all along.
- **Forcing DX11 does nothing.** The editor was confirmed to be running on `Direct3D11` and the
  delay was still 55.65 s. The uncommitted DX11 pin in `ProjectSettings.asset` solves nothing.

> **Trap: a black picture does not mean nothing rendered.** The first round measured `frame=0` and
> brightness of zero, which looked like "decoded but not drawn"; in fact that video simply starts
> on black. Look at how the footage begins before concluding anything.

**Known limitation**: saving goes through `AssetDatabase`, which is **editor-only**. Recording
standalone on the headset would need a separate JSON/binary read-write path (marked in the code).
The expected use today is recording over Link / Air Link into the editor.

> **Since 2026-08-29 there is also `PlayScene`**: a copy of `DanceCaptureScene` with recording
> removed, putting the beat-target and guide-orb modes side by side for the player to choose
> between. This scene remains **the only entry point for recording** and is unchanged. See
> [PlayScene Modes and Beat Chart Spec.md](PlayScene%20Modes%20and%20Beat%20Chart%20Spec.md).

## 4d. Character animation (optional)

`DanceRecording.characterAnimation` takes an `AnimationClip`: the **skeletal version** of the same
performance, used to drive the dancer model in the scene. Leave it empty for no dancer motion;
everything else is unaffected.

> **2026-09-01: `DanceRecording` gained a "Stage presentation" block** (`poster`, the five chroma-key
> fields, `domeMaterial` / `domeColor`). Those fields have nothing to do with recording, and the
> recorder neither writes nor reads them - they live here because PlayScene's stage is a prefab and
> **one take is the entirety of what makes one stage differ from another**. See
> "Dance Place Stages and Standing Spec" §2.1.

It and `samples` (the controller trajectory) are **two independent sets of data**; do not treat them
as one thing:

| | Source | Drives | Reference frame |
|---|---|---|---|
| `samples` | VR controller recording | Guide orbs, beat positions | The head at record time |
| `characterAnimation` | External mocap FBX | The dancer model's skeleton | The model's own root node |

They are **not necessarily the same length**, and nothing guarantees they are on the same beat. How
they are used is in
[PlayScene Modes and Beat Chart Spec.md](PlayScene%20Modes%20and%20Beat%20Chart%20Spec.md) §6.

## 5. Playback

- `DancePlayer` samples the recording against dspTime, **lerping positions and slerping
  rotations**, so playback frame rate is independent of record frame rate.
- Each hand has a proxy. **Since 2026-08-29 the proxy is a Guide Orb** (glowing sphere + particle
  trail, see [Guide Orb Spec.md](Guide%20Orb%20Spec.md)); the old box proxies were **deleted** on
  2026-08-29.
- There are also two `LineRenderer`s (`Left Path` / `Right Path`). **Since 2026-08-29 they are no
  longer drawn by `DancePlayer`**; `GuideOrb` draws **the player's own controller trajectory**
  instead - recording starts when a hand enters the orb and stops one second after it leaves. The
  orb already performs the recorded trajectory, so drawing the player's hand is the only thing that
  is new information. Details in [Guide Orb Spec.md](Guide%20Orb%20Spec.md) §6b. The code in
  `DancePlayer` that drew the recorded path (`leftPath`/`rightPath`/`BuildPathWindow` and so on) has
  been removed entirely, so two components cannot fight over the same line.
- Looping: **the reference frame is not re-snapshotted**, so every pass lands in exactly the same
  place. The proxies are hidden between passes, so the trail does not draw a straight line from the
  end back to the start.
- To switch recordings: `LoadRecording(recording)`, or just swap the asset in the Inspector.

## 6. Scene

`Assets/Scenes/DanceCaptureScene.unity` - copied from `SampleScene`, with everything beat-related
(`BeatSpawner`, both test harnesses, the `Beat Hit Volume` on the controllers) **disabled rather
than deleted**, so it stays comparable to the main scene and can be switched back on when needed.

## 7. Architectural constraints

- **Completely decoupled from `BeatTarget`**: recording and playback know nothing about beats. This
  is the **input** to a future beat generator, not part of the gameplay.
- **No XR types are referenced**: `DanceRecorder` / `DancePlayer` take plain `Transform`s. Swapping
  rigs (XRI → Meta XR SDK) does not touch the core code - the same approach as `HandTouchSource`.
- `DanceRecording.TrySample(time, out sample)` is an interface **beat extraction will reuse
  directly** - finding beats in a trajectory is, in essence, sampling that function densely.

## 8. Next steps (not implemented)

- Extract beats from the trajectory: minima in controller speed / sudden changes of direction, or
  fused with the music's onsets
- Height / arm-span normalisation
- Aligning music with the recording: today the start and stop are manual keypresses, not tied to
  music playback
- Recording standalone on the headset (needs the JSON storage path)

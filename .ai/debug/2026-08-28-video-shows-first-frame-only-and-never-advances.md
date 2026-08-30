# Video shows a single static frame and never advances, with no errors

**2026-08-28 ・ cost: a long session of wrong hypotheses ・ status: SOLVED 2026-08-29 - the latency is the OS H.264 decode path; transcoding the clip to VP8 on import removes it. See the update at the foot of this file**

## Symptom

- A `VideoPlayer` shows the first frame and then nothing: no motion, no sound.
- **`isPrepared = true`, `isPlaying = true`, and zero errors in the console.** Everything
  reports healthy.
- `time` stays exactly `0.00` and `frame` stays `-1` indefinitely.
- Metadata reads back perfectly - resolution, frame count, frame rate, duration, audio track
  count - so the container is being parsed fine.
- `prepareCompleted` and `started` both fire. `frameReady` fires **once, for frame 0**, and
  never again.
- In a minimal scene, `AudioSampleProvider buffer overflow. N sample frames discarded.`
  appears repeatedly. That is a *symptom*, not a cause - the audio decoder runs ahead while
  picture presentation is stalled.

## What was actually true

**This machine's `VideoPlayer` takes about 18 seconds to deliver its first picture. After
that it plays perfectly, in exact realtime.** Nothing was broken; it was slow, in a way that
looked exactly like broken.

```
empty scene, 1920x1080 H.264, playOnAwake, CameraNearPlane, no project scripts,
audioOutputMode = None:

[Latency] t+0.0s   time=0.00 frame=-1     <- isPrepared already true
[Latency] t+2.0s   time=0.00 frame=-1
   ... unchanged all the way through ...
[Latency] t+18.0s  time=0.00 frame=-1
[Latency] *** PICTURE STARTED at t+18.08s ***
[Latency] t+20.0s  time=1.80 frame=54
[Latency] t+22.0s  time=3.80 frame=114
[Latency] t+24.0s  time=5.80 frame=174
[Latency] t+26.0s  time=7.80 frame=234
[Latency] t+28.0s  time=9.80 frame=294
```

Once started: 2.00s of video per 2.00s of wall clock. Zero drift.

**Why the capture scene never played, when the minimal scene eventually did.** The take was
12.5 seconds long and `DancePlayer` reset the video playhead at the start of every loop -
`Stop()`, reassign, `Prepare()`. Each reset threw away the buffered decoder state and
restarted the 18-second climb, and the loop came round again after 12.5. The startup could
never finish. The picture was frozen on frame one *forever*, by construction.

The same trap in the recorder: it gated the countdown on `isPrepared`, which goes true within
milliseconds - roughly 18 seconds before a picture exists.

## Why it was hard

Every plausible cause was wrong, and everything reported healthy. Each of these was tested
and eliminated:

| Ruled out | On what evidence |
|---|---|
| An undecodable audio track stalling the player | `audioOutputMode = None`: the 18s latency was **identical**. This also killed the buffer-overflow theory |
| Unsupported source codec | Transcoding to H264 and reimporting: identical |
| The RenderTexture output path | Switched to `CameraNearPlane`: identical |
| A non-standard resolution (the first clip was 1724x1080) | An unrelated 1920x1080 H.264 clip behaved identically |
| Anything in this project's code | A brand-new empty scene - default camera plus one `VideoPlayer` - reproduced it |
| The Editor not rendering, so the decoder never ticks | `Time.renderedFrameCount` advanced in lockstep with `Time.frameCount` |
| Editor throttling in the background | `Application.isFocused = true`, `runInBackground = true`, `targetFrameRate = -1` |
| Broken system decoders | Windows' own Movies & TV plays the same files fine |

**The measurement window was the real obstacle.** Every early probe ran for 7-8 seconds, and
the thing being measured took 18. Every one of those probes reported `frame=-1` and was read
as "it never decodes" - a conclusion the data could not actually support. Two claims were
made and retracted on that basis, including "forcing DX11 did not help", which was never
tested over a long enough window to know either way.

## Two-minute path next time

| Instrument | Reading that means this |
|---|---|
| `Assets/Scenes/MinimalVideoTest.unity` | Kept for exactly this. Empty scene, one VideoPlayer. If it is slow here, the fault is below the project - do not go looking in the capture code |
| **A 30-second probe, not an 8-second one** | Register an `EditorApplication.update` callback logging once per second. A short window cannot distinguish "never" from "slow" |
| `vp.frame` | `-1` means *no picture has ever been delivered*. `isPrepared` only proves the container was parsed - it is **not** a readiness signal and must never be used as a start gate |
| Any code path calling `vp.Stop()` | Stop discards buffered decoder state. On a slow decoder, anything that stops and re-prepares per loop can never produce a picture at all |
| Windows **Movies & TV** on the same file | Not VLC or PotPlayer - those ship their own decoders. Proves whether the OS decoders are fine |

## Lesson

**A negative result from a probe shorter than the latency you are hunting is not a negative
result.** `frame=-1` at t+8s and `frame=-1` at t+30s are entirely different findings, and for
hours only the first was ever collected - which made a slow component look like a dead one and
sent the search after codecs, graphics APIs and third-party decoders that were never involved.

Second: `isPrepared` and `isPlaying` are not evidence that a video is decoding. `frame` is.

## Outcome

`VideoPlayer` was kept. The startup cost is now paid **once per session, off the critical
path**, by a component that owns the decoder's lifecycle and never tears it down -
`Assets/Scripts/DanceCapture/DanceVideoScreen.cs`. See
[../decisions/video-playback-decoder.md](../decisions/video-playback-decoder.md).

Why the decoder needs 18 seconds on this machine specifically is still unexplained. It no
longer blocks anything, so it was not chased further.

---

# 2026-08-29 update - it was the H.264 decode path, and it is fixable

Chasing it again for a second clip turned up both the cause and a one-setting fix.

## The number was never stable, which is why it read as mysterious

| Clip / setting | First picture |
|---|---|
| `DrumDemo.mp4`, empty scene (2026-08-28) | **~18s** |
| `LaunchPad Compassion Dance test.mp4`, PlayScene | **55.65s** |
| Same clip, **importer transcoding to VP8** | **1.76s** |

Same machine, same day, same editor for the last two. **A "slow decoder" that varies from 18s
to 56s is not a fixed startup cost** - and treating the 18s as a constant to be designed around
is what stopped the search a day early.

## Cause

Unity's `VideoPlayer` decodes H.264 through the OS (Media Foundation on Windows). That path is
what is slow here. Setting the importer to **transcode to VP8** makes Unity re-encode the clip
at import time and decode it with its own built-in decoder, so the OS path is never used.

`Assets/Video/LaunchPad Compassion Dance test.mp4.meta` now carries
`enableTranscoding: 1, codec: 2` (VP8, High quality). The setting lives in the `.meta`, so it
travels with the asset and collaborators get the same behaviour without being told.

After transcoding: first picture at 1.76s, then **exactly 5.00s of video per 5s of wall clock**
measured to t+45s, and the `AudioSampleProvider buffer overflow` warnings stop - they were the
audio decoder running ahead of a stalled picture, exactly as the original write-up guessed.

## Two claims from the original investigation, now settled

**"Forcing DX11 did not help" - correct, and this time properly measured.** The original note
flagged that claim as never tested over a long enough window. The editor was confirmed running
`Direct3D11` (`SystemInfo.graphicsDeviceType`, logged at t+0) and the latency was still 55.65s.
The uncommitted DX11 pin in `ProjectSettings.asset` buys nothing.

**There was never a renderer fault.** Reading the `RenderTexture` back into a `Texture2D` each
frame and averaging showed luminance moving as soon as decoding began. The picture reaches the
RT, the material samples it, the quad draws it. Every part downstream of the decoder was always
working.

## Two traps that cost time here

**A black RenderTexture is not proof of a broken render path.** The first run showed
`frame=0` with `rtLum avg=0 min=0 max=0` and looked like "decodes but renders nothing". The
clip simply opens on several seconds of black - luminance was still 0 at t=4.21s of *playing*
video, and only rose once the picture brightened. Check what the clip actually starts with
before concluding the RT is dead.

**`ffmpeg -avoid_negative_ts make_zero` can make the start time non-zero.** Re-encoding to
clean up timestamps produced `start_pts=263` (0.0214s), and Unity said so at import:
`First video frame not zero: 1 (0.0213216s). Result may be out of sync.` Removing that one flag
gave `start_pts=0` on both streams. Isolated by encoding 3-second samples with and without it.

## Two-minute path next time

| Instrument | Reading that means this |
|---|---|
| **Importer: transcode to VP8** | Try this *first*. It is one setting, it is stored in the `.meta`, and here it took 55.65s down to 1.76s |
| `SystemInfo.graphicsDeviceType` logged at t+0 | Proves which API is actually live, rather than which one the project settings request |
| `RenderTexture` → `Texture2D.ReadPixels`, log mean luminance | Separates "the decoder is stalled" from "the picture is not reaching the screen". Only this distinguishes them |
| The clip's own opening frames | Black output can be the content, not a fault |

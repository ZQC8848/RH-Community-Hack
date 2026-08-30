# Keep Unity's VideoPlayer; own its lifecycle instead of replacing it

**2026-08-28 ・ status: DECIDED, amended 2026-08-29 ・ scope: the video shown during recording and review, not audio and not the motion capture itself**

> **2026-08-29 amendment.** The startup cost is not a fixed property of `VideoPlayer` - it is the
> OS H.264 decode path. Setting the importer to **transcode to VP8** took the first picture from
> 55.65s to **1.76s** on the same machine. `DanceVideoScreen` and its never-`Stop()` rule stay
> exactly as decided below and are still what makes warm-up cheap; transcoding just makes the
> warm-up short enough that it barely matters. **Set every new clip to transcode to VP8.**

## What forced the choice

The video screen showed its first frame and nothing more - no motion, no sound, no errors.
It looked like Unity's `VideoPlayer` was failing to decode outright, which is why three
replacement decoders got evaluated.

It was not failing. **It takes ~18 seconds on this machine to deliver its first picture, then
plays in exact realtime with zero drift.** Measured in an empty scene with no project code
and with audio disabled, so neither this project nor the audio pipeline is involved. Full
write-up:
[../debug/2026-08-28-video-shows-first-frame-only-and-never-advances.md](../debug/2026-08-28-video-shows-first-frame-only-and-never-advances.md).

That reframes the problem completely. There was never a decoding fault to replace - there was
a startup cost that the code was paying over and over and never letting finish.

## Decision

**Stay on `VideoPlayer`.** Put its lifecycle behind one component,
`Assets/Scripts/DanceCapture/DanceVideoScreen.cs`, which enforces a single rule:

> **Never call `VideoPlayer.Stop()`.** Stop discards buffered decoder state, which means
> paying the startup cost again. Everything that wants to "stop" uses `Pause()` plus a seek
> back to position (`Park()`).

Three consequences:

1. **Warm up at scene load.** The clip starts buffering the moment the scene runs, so the
   wait is spent while the dancer is putting the headset on. By the time X is pressed there is
   usually nothing to wait for.
2. **Gate on `frame >= 0`, never on `isPrepared`.** `isPrepared` goes true about 18 seconds
   before a picture exists. Using it as the start gate is what let the countdown - and the
   recorded motion timeline - begin against a video that had not started.
3. **Never rebuild the decoder per loop.** Restarting a pass seeks; it does not stop and
   re-prepare. `DancePlayer.Restart Video Each Loop` can be turned off entirely for a take much
   shorter than its video.

No scene wiring: `DanceVideoScreen.For(videoPlayer)` attaches the component on demand.

## Why not the alternatives

| Alternative | Its case | Why not |
|---|---|---|
| **[VLC for Unity](https://github.com/videolan/vlc-unity)** (VideoLAN/Videolabs) | Brings LibVLC's own decoder. Production-grade, actively maintained, LGPL 2.1 source | **$700/year**; the free trial watermarks the video and cuts playback at 60s per session. And there is now no decoding fault to fix - it would buy nothing |
| **[ViveMediaDecoder](https://github.com/ViveSoftware/ViveMediaDecoder)** (HTC) | Free, open source, FFmpeg-based, Windows + VR | Four blockers, below. Also buys nothing now |
| **Replace nothing, change nothing** | Zero work | The 18s cost still lands on whoever presses X, and the per-loop reset still guarantees a frozen picture |

### Why ViveMediaDecoder was rejected on inspection

1. **Its FFmpeg dependency is unobtainable from the documented source.** The readme sends you
   to `ffmpeg.zeranoe.com` for FFmpeg **3.4** shared builds; that site shut down in September
   2020 and the plugin needs those specific 2017-era versions. The DLLs in the repo (~460 KB)
   are only the wrapper.
2. **DX11 only.**
3. **Its `YUV2RGBA` shader is built-in-pipeline era (2019).** This project is URP, where such
   a shader renders magenta.
4. **Path-based API.** `initDecoder(string path)` takes a file path, not a `VideoClip`, so
   `DanceRecording.video` would have to change its data model.

Last commit February 2019; states "Unity 5 and later", untested on Unity 6000.3.

## What this rests on

| Assumption | Status | Evidence, or the check that would settle it |
|---|---|---|
| Once started, playback is realtime and does not drift | **verified** | 2.00s of video per 2.00s of wall clock across an 8-second sample |
| Pausing and seeking is materially cheaper than Stop + Prepare | **unverified - the load-bearing assumption** | If a seek turns out to cost another full warm-up, `IsReadyFor` still holds the pass until a picture exists, so the failure mode is slowness, not desync. Watch the `[DanceVideoScreen] ready after Ns` log |
| The 18s latency is specific to this machine | unverified | Run `MinimalVideoTest` on a collaborator's machine. Worth knowing, but no longer blocking - the warm-up absorbs it either way |
| Video and motion actually line up in the headset | unverified | Record a take with video on and watch it back. The music path measures 13 microseconds of drift, but `VideoPlayer` follows the render loop, not the dsp clock |

## Accepted costs

- The first video-backed take of a session waits for the warm-up if X is pressed early. The UI
  shows `BUFFERING VIDEO...` with a running second count so it does not look hung.
- Graphics API is currently pinned to **Direct3D11** (`useDefaultGraphicsAPIs = False`, order
  `[D3D11, D3D12]`) from the DX12 hypothesis. **That hypothesis was never validly tested** -
  the probe window was shorter than the latency. Rollback values if it should go back:
  `useDefault = True`, order `[D3D12, D3D11]`. Cheap to settle: pin DX12, run
  `MinimalVideoTest` with a 30s probe, compare the startup number.

## What would reverse this

- A seek costs a full warm-up too → the per-loop restart has to go, and takes must align to a
  freely-running video rather than the other way round
- A collaborator's machine shows the same 18s startup → worth explaining rather than absorbing,
  because it would then be a Unity-wide problem, not a local quirk
- The warm-up turns out not to survive Play-mode entry/exit in a way that matters → revisit

---

## When this is superseded

*(current)*

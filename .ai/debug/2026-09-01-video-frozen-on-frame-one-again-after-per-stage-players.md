# A video frozen on frame one, for a completely different reason

**2026-09-01 ・ PlayScene, guide mode**

## Symptom

The screen shows the first frame of the take's video and nothing else. Everything around it
looks healthy:

- the take plays — `DancePlayer.IsPlaying` true, playhead advancing normally
- the dancers dance, the panel updates, the stage is occupied
- the screen renderer is enabled and holds a live RenderTexture
- **no errors and no warnings**, in the console or the Editor log

Measured on a stage with real content:

```
current = Stage 4 - 2016, mode = Guide
DancePlayer  IsPlaying=True  playhead=7.64  hasSamples=True  Screen=Video Source
video        clip=Video1  frame=0  time=0.00  playing=False  paused=True
```

The take is seven and a half seconds in. The video is paused on frame zero.

## Why it was hard

**This project has seen this exact symptom before, from an unrelated cause.** In August it was
the OS H.264 decode path taking 18-56s to produce a first picture
([2026-08-28](2026-08-28-video-shows-first-frame-only-and-never-advances.md)), fixed by
transcoding to VP8. Everything about the surface reading is identical, so the obvious move is to
re-check the importer settings — which are fine, and which cost time to confirm.

The distinguishing evidence is `frame=0`, not `frame=-1`. A decoder that has not delivered a
picture yet reports `-1`. Zero means the picture arrived, the warm-up completed and the player
was parked exactly as designed. Nothing was waiting on the decoder at all; nothing ever asked it
to resume.

## What was true

`DancePlayer.Update` resumed the video through the **private cached field `screen`**:

```csharp
if (videoStartPending && screen != null && screen.IsReadyFor(recording.video))
```

`screen` is only ever assigned by `DanceVideoScreen.For(videoPlayer)`, from the serialized
`videoPlayer` field. When each stage got its own VideoPlayer, the scene's shared `Video Source`
was deleted and that field was cleared on purpose — the screen now arrives at runtime through the
new `DancePlayer.Screen` property, pushed by `PlayModeController.EnterStage`.

`EnsureScreen()` and one call site in `StartPass` were updated to prefer the property. **Two uses
in `Update` were not.** So `screen` stayed null forever, the condition could never be true,
`Resume()` was never called, and `videoStartPending` stayed true for the life of the pass.

It is silent by construction: the flag going unserviced has no failure path, and every other
component reports success because every other component genuinely succeeded.

## Two-minute path next time

1. **Read `frame`, not `isPlaying`.** `-1` is a decoder that has not started — suspect the
   importer and the H.264 path. `0` with `paused=true` is a decoder that started, finished
   warming and is waiting for someone to resume it — suspect the caller.
2. If it is `0`, check who owns the screen. In PlayScene that is
   `DancePlayer.Screen`, pushed in `PlayModeController.EnterStage`; the serialized `videoPlayer`
   field is deliberately empty and `DancePlayer.screen` is expected to be null.
3. `DancePlayer` now warns after 8 seconds of a pending video start, naming which of the two
   cases it is. If that warning is not in the console, this is not the fault you are looking at.

## The general shape

Adding a runtime override for something previously read from a serialized field means finding
**every** read, not the ones on the path being tested. A grep for the field name is the whole
check, and it takes seconds: `grep -n "\bscreen\b"` would have shown three unconverted uses.

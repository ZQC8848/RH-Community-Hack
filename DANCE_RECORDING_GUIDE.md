# Dance Recording Guide

Thanks for helping capture motion data for this project.

You'll put on a Quest headset, dance to a track, and the project records where your
controllers travelled. Those recordings become the beat charts for a VR rhythm game — so
what we need from you is **good movement**, not technical work.

You do **not** need Unity experience. Follow the steps below in order.

**Questions, or anything unexpected?** Contact Qinchuan before changing things:
**qinchuan@usc.edu** · **213-561-9451**

---

## What you'll need

- A **Meta Quest** headset and both Touch controllers
- A **Quest Link** cable (or Air Link) to connect the headset to your PC
- **Unity 6.3 LTS — version `6000.3.14f1` exactly.** Other versions may fail to open the
  project or behave differently. Install it through Unity Hub.
- Enough clear floor space to move your arms freely without hitting anything

---

## 1. Open the project

Open the project folder in Unity Hub using **Unity `6000.3.14f1`**.

The first open takes a while — Unity has to import everything. Let it finish before doing
anything else.

## 2. Open the recording scene

In the **Project** window: `Assets` → `Scenes` → **`DanceCaptureScene`** (double-click it).

> Make sure you open **DanceCaptureScene**, not `SampleScene`. `SampleScene` is the game
> itself and does not record anything.

## 3. Set up the XR runtime

The project needs Unity's XR settings pointed at your headset before VR will work.

If you're comfortable with Unity: enable **OpenXR** under
`Edit → Project Settings → XR Plug-in Management`, and make sure the interaction profile
for your controllers (**Oculus Touch Controller Profile** / **Meta Quest Touch Plus
Controller Profile**) is enabled under the **OpenXR** section.

If you're not — this is the fiddliest step, and you have two easy options:

- **Ask Claude** (or any AI assistant) how to configure OpenXR for Meta Quest in Unity 6
- **Let Claude Code do it for you** — open this project folder with Claude Code and ask it
  to set up the XR runtime for Quest

> ⚠️ If the controller models appear frozen in place or missing once you're in VR, this
> step is almost always the cause — the interaction profile isn't enabled. Fixing that
> setting resolves it.

## 4. Record

1. Connect the headset to the PC with **Quest Link** (or Air Link) and make sure Link is
   *running* — you should be looking at the Link/Quest desktop view in the headset.
2. Press **Play** at the top of the Unity Editor.
3. Put the headset on. You'll see a floating panel in front of you with instructions.
4. **Stand where you intend to dance, facing the direction you'll face**, then press
   **X** (left controller).
5. A **3-second countdown** appears. Pressing X again during the countdown cancels it.
6. Dance. The panel shows a red `● RECORDING` readout with elapsed time.
7. Press **X** again to stop. The panel confirms `SAVED` with the file name and length.
8. Repeat for as many takes as you like — each one saves to its own file.
9. When you're done, **press Play again to stop the Editor running.**

Your recordings are saved as ScriptableObject assets in **`Assets` → `DanceRecordings`**,
named with the date and time, e.g. `Dance_2026-08-26_16-20-40.asset`.

**After you stop Play, rename your files** to something descriptive —
`Chorus_ArmsWide`, `Verse1_Take3`, etc. That makes them far easier for us to work with.

> **Important — where you stand matters.** The recording anchors to your head position and
> facing direction **at the moment the countdown ends**. Get into your starting position
> *before* the countdown finishes, not after.

> **Recording only works from inside the Unity Editor** (with Play running). A built
> `.exe` cannot save recordings.

## 5. Review a take

1. In the **Hierarchy** window, click the **`Dance Capture`** object.
2. In the **Inspector**, find the **`Dance Player`** component.
3. Drag your saved recording into **`Source` → `Recording`**.
4. Press **Play**. Two coloured boxes replay your hand motion, with trails and a line
   showing the full path.
5. **Hold `B`** (right controller) for **3 seconds** to re-anchor the playback in front of
   where you're currently standing. The panel shows a progress bar while you hold it.

## 6. Record more takes

While **`Source` → `Recording`** has a file in it, the scene is in **review mode** — the
panel shows `▶ PLAY MODE` and the X button does nothing.

**To record again, set `Source` → `Recording` back to `None`** (click the field and press
Delete, or drag the reference out). The panel returns to `● RECORD MODE` and X works again.

## 7. Record with music (optional)

If you want to dance to a track:

1. Drag your audio file (`.mp3`, `.wav`, …) into **`Assets` → `Music`**.
2. Click the **`Dance Capture`** object in the Hierarchy.
3. In the **Inspector**, find the **`Dance Recorder`** component.
4. Drag your audio into **`Music (Optional)` → `Music Clip`**.
5. Press Play and record as usual — the music starts exactly when the countdown ends.

The track is remembered inside the recording, so it plays back automatically when you
review the take too. Leave the field empty to record in silence.

> Please only use music you have the right to use. Tell us which track you used.

## 8. Sending your recordings back

**Only commit these two folders:**

- `Assets/DanceRecordings/` — your takes
- `Assets/Music/` — any audio you added

> **If you added audio, install [Git LFS](https://git-lfs.com) first** and run `git lfs
> install` once. This repo stores audio through LFS, and without it your `.mp3` will be
> committed as a broken placeholder instead of the real file. Recordings themselves are
> plain text and need nothing special.

```bash
git add Assets/DanceRecordings Assets/Music
git commit -m "Add dance recordings"
git push
```

**Please do not commit anything else.** Unity touches a lot of files just by opening a
project (settings, library caches, the scene file). Committing those makes it hard for us
to tell your recordings apart from incidental noise. `git add` the two folders by name, as
above, rather than `git add .` or `git add -A`.

Check what you're about to commit before pushing:

```bash
git status
```

If you can't use git, just zip the `Assets/DanceRecordings` folder (and `Assets/Music` if
you added audio) and send it over.

> **Please don't change any code, scene objects, or interaction logic.** If something
> seems broken or you think a change is needed, contact Qinchuan first —
> **qinchuan@usc.edu** · **213-561-9451**.

---

## Controls at a glance

| Control | What it does |
|---|---|
| **X** (left controller) | Start recording (3s countdown) · cancel countdown · stop & save |
| **Hold B** (right controller, 3s) | Re-anchor playback to where you're standing now |

## Panel states

| Panel shows | Meaning |
|---|---|
| `● RECORD MODE` | Ready — press X to record |
| `GET READY` + number | Countdown running; X cancels |
| `● RECORDING` (red) | Capturing; X stops and saves |
| `SAVED …` | Take written to `Assets/DanceRecordings` |
| `▶ PLAY MODE` (blue) | Reviewing a take; clear `Recording` to record again |
| `RECALIBRATING ORIGIN` (green) | Keep holding B |

## Troubleshooting

| Problem | Fix |
|---|---|
| Controllers invisible or frozen in place | XR interaction profile isn't enabled — see step 3 |
| Nothing happens when pressing X | You're in review mode. Set `Source` → `Recording` to `None` (step 6) |
| `NOT SAVED — Take was too short to save` | The take needs at least a moment of motion; record for longer |
| No headset view at all | Quest Link isn't running, or OpenXR isn't enabled (step 3) |
| A recording appeared that you didn't make | The keyboard `X` key also triggers recording while the Game view has focus. Harmless — just delete the file |
| Music doesn't play | Check the clip is in `Music (Optional)` → `Music Clip` on **Dance Recorder** (step 7) |

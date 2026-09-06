using System;
using UnityEngine;
using RHCommunityHack.Environment;

namespace RHCommunityHack.Play
{
    // Runs the prologue, then hands the piece over to TimelineDirector.
    //
    // The script opens before the timeline exists: "we are born into a completely black space", a
    // narrator begins telling IFEL's story, and objects appear around us as she names them - AR
    // glasses, immersive cameras, a 5G tower, then the soft technologies. Only afterwards do the
    // objects slide away and the ground timeline count back to 1964, leaving you standing at the
    // start of the zigzag.
    //
    // So the prologue is NOT a stage. It happens off the line, at its own anchor, with the line
    // drawn to zero length. Handing over is therefore a natural fit for the existing loop: see
    // Finish, which starts TimelineDirector at index -1 so its first Grow phase draws the line
    // out from nothing to the first stage before carrying the player there. That IS the script's
    // "the timeline stretches out from our feet".
    //
    // ---------------------------------------------------------------------------------------
    // THE NARRATION SLOT
    //
    // There is no recorded voiceover yet. Every cue has an AudioClip field that is null today,
    // and `CueComplete` is the ONE method that has to change when the recordings arrive - in
    // fact it already handles them:
    //
    //     clip assigned  -> the cue lasts as long as the clip, plus holdAfter
    //     clip null      -> the cue lasts `seconds`
    //
    // which means a half-recorded prologue works, and dropping clips in one at a time needs no
    // code at all. Assign the AudioSource below and the clips on the cues; nothing else moves.
    // ---------------------------------------------------------------------------------------
    [DisallowMultipleComponent]
    public class PrologueDirector : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Where the player stands for the prologue. Must be far enough from every stage " +
                 "that DancePlaceManager claims none of them - otherwise a dome lights up behind " +
                 "the narration.")]
        [SerializeField] Transform playerAnchor;

        [SerializeField] Unity.XR.CoreUtils.XROrigin origin;
        [SerializeField] SubtitleDisplay subtitles;
        [SerializeField] ScreenFade fade;

        [Tooltip("Handed control when the prologue ends. Its Auto Start must be OFF, or it will " +
                 "start walking the timeline underneath the narration.")]
        [SerializeField] TimelineDirector timeline;

        [Tooltip("RESERVED for the narration. Plays each cue's voice clip. Safe to leave empty " +
                 "until the recordings exist - the cues fall back to their timers.")]
        [SerializeField] AudioSource voiceSource;

        [Header("Content")]
        [SerializeField] PrologueCue[] cues = new PrologueCue[0];

        [Header("Opening")]
        [Tooltip("Seconds of black before the first line. The script opens in darkness, and " +
                 "arriving mid-sentence with the headset still settling reads as a bug.")]
        [SerializeField, Min(0f)] float openInDarkSeconds = 2f;

        [Tooltip("Seconds to fade up from black at the start, and down again at the hand-over.")]
        [SerializeField, Min(0f)] float fadeSeconds = 1.2f;

        public bool IsRunning { get; private set; }
        public int CueIndex => index;

        // Fires when the last cue has played and control has passed on. Anything that needs to
        // wait for the prologue hangs off this rather than polling.
        public event Action OnFinished;

        enum Phase { OpeningDark, FadingUp, Cue, Handover, Done }

        Phase phase = Phase.OpeningDark;
        int index = -1;
        float elapsed;
        float cueLength;
        float voiceEndedAt = -1f;   // cue-relative time the clip stopped; -1 while it still plays
        bool staged;

        void Start()
        {
            if (cues.Length == 0)
            {
                Debug.LogWarning("[PrologueDirector] No cues - handing straight over.", this);
                Finish();
                return;
            }

            IsRunning = true;
            if (subtitles != null) subtitles.Hide();
            if (fade != null) fade.SetOpacity(1f);

            MovePlayerToAnchor();
            phase = Phase.OpeningDark;
            elapsed = 0f;
        }

        void MovePlayerToAnchor()
        {
            if (origin == null || playerAnchor == null) return;

            // Turn first, then move: rotating the rig swings the camera around the rig origin, so
            // doing it afterwards would undo the alignment. Same order as TimelineDirector.
            Vector3 forward = playerAnchor.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 1e-6f)
                origin.MatchOriginUpCameraForward(Vector3.up, forward.normalized);

            Vector3 target = playerAnchor.position;
            target.y = origin.Camera != null ? origin.Camera.transform.position.y : target.y;
            origin.MoveCameraToWorldLocation(target);
        }

        void Update()
        {
            if (phase == Phase.Done) return;
            elapsed += Time.deltaTime;

            switch (phase)
            {
                case Phase.OpeningDark:
                    if (elapsed >= openInDarkSeconds) { phase = Phase.FadingUp; elapsed = 0f; }
                    break;

                case Phase.FadingUp:
                {
                    float t = fadeSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeSeconds);
                    if (fade != null) fade.SetOpacity(1f - t);
                    if (t >= 1f) StartCue(0);
                    break;
                }

                case Phase.Cue:
                    TickCue();
                    break;

                case Phase.Handover:
                {
                    float t = fadeSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeSeconds);
                    if (fade != null) fade.SetOpacity(t);
                    if (t >= 1f) Finish();
                    break;
                }
            }
        }

        void StartCue(int next)
        {
            index = next;
            elapsed = 0f;
            voiceEndedAt = -1f;
            staged = false;
            phase = Phase.Cue;

            var cue = cues[index];

            if (subtitles != null) subtitles.Show(cue.speaker, cue.subtitle);

            // The narration slot. Null clip is the normal state today and is not an error.
            if (voiceSource != null && cue.voice != null)
            {
                voiceSource.Stop();
                voiceSource.clip = cue.voice;
                voiceSource.Play();
            }

            cueLength = cue.voice != null ? cue.voice.length + cue.holdAfter : cue.seconds;

            if (cue.dismissAll) DismissEverything();
        }

        void TickCue()
        {
            var cue = cues[index];

            // Staggered reveal, resolved from elapsed time rather than a coroutine so that a skip
            // or a scrub cannot leave half a group in flight.
            if (!staged)
            {
                staged = true;
                for (int i = 0; i < cue.reveal.Length; i++)
                    if (cue.reveal[i] != null) cue.reveal[i].Appear(i * cue.revealStagger);
            }

            if (!CueComplete(cue)) return;

            if (index + 1 < cues.Length) StartCue(index + 1);
            else { phase = Phase.Handover; elapsed = 0f; if (subtitles != null) subtitles.Hide(); }
        }

        // THE SEAM. Today every cue has a null clip and this is a timer; the moment a clip is
        // assigned the same cue waits for the audio instead. Nothing else in this class knows
        // which of the two is happening.
        bool CueComplete(PrologueCue cue)
        {
            if (cue.voice != null && voiceSource != null)
            {
                // holdAfter counts from the END of the line, not from the start of the cue -
                // otherwise a clip longer than holdAfter swallows the pause entirely and the
                // next line starts the frame this one stops.
                if (voiceSource.isPlaying) return false;
                if (voiceEndedAt < 0f) voiceEndedAt = elapsed;
                return elapsed - voiceEndedAt >= cue.holdAfter;
            }

            return elapsed >= cueLength;
        }

        void DismissEverything()
        {
            foreach (var cue in cues)
                foreach (var prop in cue.reveal)
                    if (prop != null) prop.Vanish();
        }

        // Jump straight to the timeline. For iterating on anything downstream without sitting
        // through the narration every run. Deliberately not bound to a key here - bind it from
        // whatever dev harness wants it.
        public void Skip()
        {
            if (phase == Phase.Done) return;
            foreach (var cue in cues)
                foreach (var prop in cue.reveal)
                    if (prop != null) prop.HideImmediately();
            if (subtitles != null) subtitles.Hide();
            Finish();
        }

        void Finish()
        {
            phase = Phase.Done;
            IsRunning = false;
            if (voiceSource != null) voiceSource.Stop();

            // Begin, not Start: index -1 makes TimelineDirector's first Grow draw the line out
            // from nothing before it carries the player to the first stage, which is the script's
            // "we are standing at the start of a zigzag timeline that stretches out from our
            // feet". It also handles the fade back up on arrival, so this does not.
            if (timeline != null) timeline.Begin();
            else if (fade != null) fade.SetOpacity(0f);

            OnFinished?.Invoke();
        }
    }
}

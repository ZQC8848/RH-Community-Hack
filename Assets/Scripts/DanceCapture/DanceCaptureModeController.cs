using UnityEngine;

namespace RHCommunityHack.DanceCapture
{
    // Decides whether the scene is recording or reviewing, using one rule: a recording assigned
    // to the DancePlayer means playback mode, an empty slot means record mode.
    //
    // This lives outside DanceRecorder on purpose. A recorder that disabled itself would stop
    // running Update and could never re-enable itself when the slot was cleared again, so the
    // switch has to be owned by something that keeps running in both modes.
    public class DanceCaptureModeController : MonoBehaviour
    {
        public enum Mode { Record, Playback }

        [SerializeField] DanceRecorder recorder;
        [SerializeField] DancePlayer player;

        public Mode CurrentMode => player != null && player.Recording != null ? Mode.Playback : Mode.Record;
        public DanceRecording LoadedRecording => player != null ? player.Recording : null;

        DanceRecording lastSeen;

        void Start()
        {
            // Seed from the current state so a take already assigned in the Inspector is not
            // treated as a fresh assignment and restarted a second time on the first frame.
            lastSeen = player != null ? player.Recording : null;
            ApplyMode();
        }

        void Update()
        {
            DanceRecording current = player != null ? player.Recording : null;

            if (current != lastSeen)
            {
                lastSeen = current;
                // Assigning or clearing the slot mid-session should just work, without needing
                // to leave play mode.
                if (current != null) player.Play();
                else player.Stop();
            }

            ApplyMode();
        }

        void ApplyMode()
        {
            if (recorder == null) return;

            // Disabled outright rather than merely ignoring input: while a take is loaded for
            // review, the recorder must not be able to start capturing at all.
            bool shouldBeEnabled = CurrentMode == Mode.Record;
            if (recorder.enabled != shouldBeEnabled) recorder.enabled = shouldBeEnabled;
        }
    }
}

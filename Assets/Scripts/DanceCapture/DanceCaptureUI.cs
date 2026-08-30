using UnityEngine;
using UnityEngine.UI;

namespace RHCommunityHack.DanceCapture
{
    // World-space status readout for the capture scene. Legacy UI.Text rather than TextMeshPro
    // on purpose: TMP's essential resources are not imported in this project, so TMP text would
    // silently render as nothing. Swap it if TMP essentials ever get imported.
    public class DanceCaptureUI : MonoBehaviour
    {
        [SerializeField] DanceRecorder recorder;
        [SerializeField] DancePlayer player;
        [SerializeField] DanceCaptureModeController mode;
        [SerializeField] Text statusText;

        [Tooltip("How long the 'saved' confirmation stays up before returning to the idle prompt.")]
        [SerializeField] float savedMessageDuration = 4f;

        static readonly Color IdleColour = Color.white;
        static readonly Color RecordingColour = new Color(1f, 0.35f, 0.35f);
        static readonly Color CountdownColour = new Color(1f, 0.85f, 0.3f);
        static readonly Color CalibrateColour = new Color(0.4f, 1f, 0.7f);
        static readonly Color PlaybackColour = new Color(0.45f, 0.8f, 1f);

        string transientMessage;
        float transientExpiry;

        void OnEnable()
        {
            if (recorder != null)
            {
                recorder.OnRecordingSaved += HandleSaved;
                recorder.OnRecordingFailed += HandleFailed;
            }
            if (player != null) player.OnOriginRecalibrated += HandleRecalibrated;
        }

        void OnDisable()
        {
            if (recorder != null)
            {
                recorder.OnRecordingSaved -= HandleSaved;
                recorder.OnRecordingFailed -= HandleFailed;
            }
            if (player != null) player.OnOriginRecalibrated -= HandleRecalibrated;
        }

        void HandleSaved(DanceRecording recording)
        {
            string music = recording.music != null ? recording.music.name : "no music";
            transientMessage = $"SAVED  {recording.name}\n" +
                               $"{recording.capturedDuration:F1}s  at  {recording.averageSampleRate:F0} Hz  •  {music}";
            transientExpiry = Time.time + savedMessageDuration;
        }

        void HandleFailed(string reason)
        {
            transientMessage = $"NOT SAVED\n{reason}";
            transientExpiry = Time.time + savedMessageDuration;
        }

        void HandleRecalibrated()
        {
            transientMessage = "ORIGIN RECALIBRATED\nPlayback re-anchored to where you are standing";
            transientExpiry = Time.time + 2.5f;
        }

        void Update()
        {
            if (statusText == null) return;

            // Holding B takes priority: the player needs to see the hold filling up, otherwise
            // there is no way to tell a 3-second hold from a button that did nothing.
            if (player != null && player.RecalibrateProgress > 0f)
            {
                float hold = player.RecalibrateHoldSeconds;
                float remaining = Mathf.Max(0f, hold - player.RecalibrateProgress * hold);
                statusText.color = CalibrateColour;
                statusText.text = $"RECALIBRATING ORIGIN\nKeep holding B...  {remaining:F1}s\n\n{ProgressBar(player.RecalibrateProgress)}";
                return;
            }

            // Playback mode owns the whole readout: with a take loaded the recorder is disabled,
            // so showing "Press X to record" would be an instruction that does nothing.
            if (mode != null && mode.CurrentMode == DanceCaptureModeController.Mode.Playback)
            {
                statusText.color = PlaybackColour;
                statusText.text = "▶ PLAY MODE"
                                + PlaybackLine()
                                + "\n\nClear the Recording field on Dance Player to record again"
                                + HoldHintFooter();
                return;
            }

            if (recorder != null && recorder.IsPreparingVideo)
            {
                statusText.color = CountdownColour;
                // The elapsed count matters: the decoder can take many seconds to deliver its
                // first picture, and a message that never changes through that looks hung.
                statusText.text = $"BUFFERING VIDEO...  {recorder.VideoBufferingSeconds:F0}s\n\n" +
                                  "The countdown starts once the video is ready\n\nPress X to cancel";
                return;
            }

            if (recorder != null && recorder.IsCountingDown)
            {
                statusText.color = CountdownColour;
                statusText.text = $"GET READY\n\n{Mathf.CeilToInt(recorder.CountdownRemaining)}\n\nPress X to cancel";
                return;
            }

            if (recorder != null && recorder.IsRecording)
            {
                statusText.color = RecordingColour;
                statusText.text =
                    $"● RECORDING   {FormatTime(recorder.ElapsedSeconds)}\n" +
                    $"{recorder.SampleCount} samples\n\n" +
                    "Press X to stop and save";
                return;
            }

            statusText.color = IdleColour;

            if (Time.time < transientExpiry)
            {
                statusText.text = transientMessage + "\n\nPress X to record again";
                return;
            }

            string accompaniment = "";
            if (recorder != null)
            {
                if (recorder.HasVideo && recorder.HasMusic) accompaniment = "  (video + music will play)";
                else if (recorder.HasVideo) accompaniment = "  (video will play)";
                else if (recorder.HasMusic) accompaniment = "  (music will play)";
            }

            statusText.text = "● RECORD MODE\n\nPress X to start recording" + accompaniment;
        }

        // Sits at the bottom of the PLAY MODE panel only. Recalibrating re-anchors the playback
        // origin, so the hint would be pointing at nothing while recording - the recorder
        // captures its own frame when a take begins.
        string HoldHintFooter() => player != null
            ? $"\n\n————————————————\nHold B for {player.RecalibrateHoldSeconds:0.#}s to recalibrate origin"
            : "";

        string PlaybackLine()
        {
            if (player == null || player.Recording == null) return "";

            if (!player.HasCalibratedOrigin) return $"\n\nLoaded: {player.Recording.name}  (origin not calibrated yet)";

            return player.IsPlaying
                ? $"\n\nPlaying: {player.Recording.name}\n{FormatTime(player.PlayheadSeconds)} / {FormatTime(player.TrimmedDuration)}"
                : $"\n\nLoaded: {player.Recording.name}  (stopped)";
        }

        static string ProgressBar(float fill01)
        {
            const int width = 20;
            int filled = Mathf.RoundToInt(Mathf.Clamp01(fill01) * width);
            return "[" + new string('#', filled) + new string('-', width - filled) + "]";
        }

        static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float remainder = seconds - minutes * 60f;
            return $"{minutes}:{remainder:00.0}";
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using RHCommunityHack.DanceCapture;

namespace RHCommunityHack.Play
{
    // World-space status readout for the play scene: which mode is running, what it is playing,
    // and how the other mode is reached.
    //
    // Legacy UI.Text rather than TextMeshPro on purpose, matching DanceCaptureUI: TMP's essential
    // resources are not imported in this project, so TMP text would silently render nothing.
    //
    // This is also where DanceFollowScore's numbers finally surface - until now the follow rate
    // was computed every frame and read by nobody.
    public class PlayModeUI : MonoBehaviour
    {
        [SerializeField] PlayModeController controller;
        [Tooltip("Guide-mode follow rate. Lives under the Guide Mode group, so it stops " +
                 "updating in beat mode and its last-pass numbers stay frozen and readable.")]
        [SerializeField] DanceFollowScore followScore;
        [Tooltip("Left empty in PlayScene: each stage owns its own world-fixed panel, and " +
                 "DancePlaceManager points this at whichever one the player is standing on.")]
        [SerializeField] Text statusText;

        // One writer, three panels. Passing null blanks the readout, which is what should happen
        // when the player is not on a stage at all.
        public void SetTarget(Text target)
        {
            if (statusText != null && statusText != target) statusText.text = "";
            statusText = target;
        }

        static readonly Color BeatColour = new Color(1f, 0.75f, 0.35f);
        static readonly Color GuideColour = new Color(0.45f, 0.8f, 1f);
        static readonly Color CalibrateColour = new Color(0.4f, 1f, 0.7f);

        void Update()
        {
            if (statusText == null || controller == null) return;

            // Holding B takes priority: without it there is no way to tell a hold in progress
            // from a button that did nothing. Read from the controller, not from DancePlayer -
            // the player is switched off in beat mode, where the gesture still has to work.
            if (controller.RecalibrateProgress > 0f)
            {
                float hold = controller.RecalibrateHoldSeconds;
                float remaining = Mathf.Max(0f, hold - controller.RecalibrateProgress * hold);
                statusText.color = CalibrateColour;
                statusText.text = $"RECALIBRATING ORIGIN\nKeep holding B...  {remaining:F1}s\n\n" +
                                  ProgressBar(controller.RecalibrateProgress);
                return;
            }

            string take = controller.Take != null ? controller.Take.name : "no take assigned";

            if (controller.CurrentMode == PlayModeController.Mode.Beat)
            {
                statusText.color = BeatColour;
                statusText.text = "● BEAT MODE\n\n"
                                + $"Charted from: {take}\n"
                                + "Hit the spheres as the ring closes in\n\n"
                                + "————————————————\n"
                                + "Press X for Guide Mode"
                                + HoldHint();
                return;
            }

            statusText.color = GuideColour;
            statusText.text = "▶ GUIDE MODE\n\n"
                            + $"Following: {take}\n"
                            + "Keep your hands inside the orbs\n"
                            + FollowLine()
                            + "\n\n————————————————\n"
                            + "Press X for Beat Mode"
                            + HoldHint();
        }

        string FollowLine()
        {
            if (followScore == null) return "";

            string live = $"\n\nFollowing now:  L {Percent(followScore.LeftRatio)}   " +
                          $"R {Percent(followScore.RightRatio)}   " +
                          $"overall {Percent(followScore.OverallRatio)}";

            if (!followScore.HasCompletedPass) return live;

            return live + $"\nLast pass:      overall {Percent(followScore.LastPassOverallRatio)}";
        }

        string HoldHint() => controller != null
            ? $"\nHold B for {controller.RecalibrateHoldSeconds:0.#}s to recalibrate origin"
            : "";

        static string Percent(float ratio01) => $"{Mathf.RoundToInt(Mathf.Clamp01(ratio01) * 100f)}%";

        static string ProgressBar(float fill01)
        {
            const int width = 20;
            int filled = Mathf.RoundToInt(Mathf.Clamp01(fill01) * width);
            return "[" + new string('#', filled) + new string('-', width - filled) + "]";
        }
    }
}

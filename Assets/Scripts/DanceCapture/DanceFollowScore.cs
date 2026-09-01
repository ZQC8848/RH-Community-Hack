using UnityEngine;
using RHCommunityHack.Interaction;

namespace RHCommunityHack.DanceCapture
{
    // Measures how much of a take the dancer actually kept their hands inside the guide orbs.
    //
    // This exists as a separate component because GuideOrb deliberately does not know that
    // recorded takes exist - it only knows how close a hand is. Take length, pass boundaries
    // and looping are capture-system knowledge, so they live here, on the side of the boundary
    // that is already bound to this project. See .ai/decisions/guide-orb-not-a-beat-target.md
    //
    // It also clears the orbs' trails on each pass boundary, being the only thing that knows
    // both where the orbs are and when a pass starts.
    //
    // Exposes numbers only. What gets shown, and where, is DanceCaptureUI's business.
    public class DanceFollowScore : MonoBehaviour
    {
        [SerializeField] DancePlayer player;
        [SerializeField] GuideOrb leftOrb;
        [SerializeField] GuideOrb rightOrb;

        // Live, for the pass currently running.
        public float LeftRatio => Ratio(leftFollowSeconds);
        public float RightRatio => Ratio(rightFollowSeconds);
        public float OverallRatio => Ratio((leftFollowSeconds + rightFollowSeconds) * 0.5f);

        // Frozen at the end of the previous pass, so a completed pass stays readable while the
        // next one is still building up.
        public float LastPassLeftRatio { get; private set; }
        public float LastPassRightRatio { get; private set; }
        public float LastPassOverallRatio { get; private set; }
        public bool HasCompletedPass { get; private set; }

        float leftFollowSeconds;
        float rightFollowSeconds;
        float passElapsed;

        void OnEnable()
        {
            if (player == null) return;
            player.OnPassStarted += HandlePassStarted;
            player.OnPlaybackFinished += FinishPass;
        }

        void OnDisable()
        {
            if (player == null) return;
            player.OnPassStarted -= HandlePassStarted;
            player.OnPlaybackFinished -= FinishPass;
        }

        void Update()
        {
            if (player == null || !player.IsPlaying) return;

            // PlayheadSeconds - not an accumulator of our own - because DancePlayer keeps
            // IsPlaying true through the pause between loops while the playhead sits at 0.
            // Counting our own deltaTime would silently add that pause to every denominator.
            float playhead = player.PlayheadSeconds;
            if (playhead <= 0f) return;

            float delta = playhead - passElapsed;
            if (delta <= 0f) return;
            passElapsed = playhead;

            if (leftOrb != null && leftOrb.IsFollowing) leftFollowSeconds += delta;
            if (rightOrb != null && rightOrb.IsFollowing) rightFollowSeconds += delta;
        }

        void HandlePassStarted()
        {
            FinishPass();

            leftFollowSeconds = 0f;
            rightFollowSeconds = 0f;
            passElapsed = 0f;

            if (leftOrb != null) leftOrb.ClearTrail();
            if (rightOrb != null) rightOrb.ClearTrail();
        }

        // Throw the running pass away WITHOUT recording it. Walking off a stage half way through
        // must not write a half-danced pass into LastPass* - which is exactly what FinishPass
        // would do, and it is the obvious-looking thing to call here.
        public void Abandon()
        {
            leftFollowSeconds = 0f;
            rightFollowSeconds = 0f;
            passElapsed = 0f;
        }

        void FinishPass()
        {
            if (passElapsed <= 0f) return;

            LastPassLeftRatio = LeftRatio;
            LastPassRightRatio = RightRatio;
            LastPassOverallRatio = OverallRatio;
            HasCompletedPass = true;
        }

        float Ratio(float followSeconds) => passElapsed > 0f ? Mathf.Clamp01(followSeconds / passElapsed) : 0f;
    }
}

using System.Collections.Generic;
using UnityEngine;
using Unity.XR.CoreUtils;
using RHCommunityHack.Environment;

namespace RHCommunityHack.Play
{
    // Walks the player along the timeline, one stage at a time, without them steering.
    //
    // This is the script's own mechanic rather than a convenience: "if the player stands still the
    // timeline advances by itself, watchable as an interactive documentary" (p.93). It also fills
    // the hole left when the teleport beacons were removed - before this, only the stage you
    // spawned on was reachable.
    //
    // The loop per stage:
    //
    //     Dwell      you are on the stage; the panel, video, dancers and gameplay are running
    //     Dissolve   the dome and its floor come apart and switch off, opening the view forward
    //     Grow       the white line extends from here to the next stage
    //     Settle     a beat to read where it went
    //     Travel     fade out, move and turn, fade in
    //
    // IT DOES NOT DECIDE WHICH STAGE IS ACTIVE. That stays with DancePlaceManager's distance
    // test: this component only moves the player, and arriving inside the enter radius makes the
    // stage take itself. Two components each believing they own "the current stage" is the thing
    // most likely to rot here, so there is deliberately only one.
    //
    // MUST live on an object nothing deactivates - same constraint as PlayModeController and
    // DancePlaceManager, and it sits with them on Play Controller.
    [DisallowMultipleComponent]
    public class TimelineDirector : MonoBehaviour
    {
        enum Phase { Dwell, Dissolve, Grow, Settle, Travel, Finished }

        [Header("Wiring")]
        [Tooltip("The XR rig to move. Position is matched by the CAMERA, not by this transform - " +
                 "the player walks around inside their play space, so the rig origin and where " +
                 "they actually are drift apart.")]
        [SerializeField] XROrigin origin;

        [Tooltip("The white line. Its stops array is the running order, so the timeline and the " +
                 "line can never disagree about which stage comes next.")]
        [SerializeField] StageTimeline timeline;

        [Tooltip("Black quad parented to the camera. Optional - without one the travel is a hard " +
                 "cut, which is legible but a 55m jump plus a turn is worth covering.")]
        [SerializeField] ScreenFade fade;

        [SerializeField] DancePlaceManager places;
        [SerializeField] PlayModeController controller;

        [Header("Timing")]
        [Tooltip("How long the player stays on a stage before it starts to come apart. THIS IS A " +
                 "PLACEHOLDER for 'until the immersive video has finished' - see StageComplete " +
                 "below, which is the one place that has to change when the video arrives.")]
        [SerializeField, Min(0f)] float dwellSeconds = 3f;

        [Tooltip("How long the dome takes to dissolve away.")]
        [SerializeField, Min(0.1f)] float dissolveSeconds = 1.5f;

        [Tooltip("How long the line takes to reach the next stage. Note this is a fixed time per " +
                 "LEG, not a speed: a leg is about 78m of path once its three folds are counted, " +
                 "so three seconds is roughly 26 m/s and the line moves fast. Worth feeling in a " +
                 "headset before trusting the number.")]
        [SerializeField, Min(0.1f)] float growSeconds = 3f;

        [Tooltip("Pause after the line arrives, before the player follows it.")]
        [SerializeField, Min(0f)] float settleSeconds = 2f;

        [Tooltip("Fade to black, then back. Each half takes this long. Ignored with no fade.")]
        [SerializeField, Min(0f)] float fadeSeconds = 0.35f;

        [Header("Travel")]
        [Tooltip("Turn the player to face the way the stage faces on arrival. The stage's facing " +
                 "is what decides where the screen and the guide orbs are, so landing backwards " +
                 "means landing with the show behind you.")]
        [SerializeField] bool matchFacing = true;

        public bool IsRunning => phase != Phase.Finished;

        readonly List<DancePlace> route = new List<DancePlace>();
        int index;
        Phase phase = Phase.Dwell;
        float elapsed;
        float growFrom, growTo;

        // Whether the move has already happened within the current Travel. An explicit flag,
        // NOT a test on elapsed: the first version detected the midpoint with
        // `elapsed < half + Time.deltaTime`, which is a window one frame wide and therefore
        // fires zero times or twice depending on how the frame times land. Twice meant index
        // advanced twice and a whole stage was skipped - measured, with Stage 3 never dissolving
        // while the other five did.
        bool arrived;

        void Start()
        {
            BuildRoute();
            if (route.Count == 0)
            {
                Debug.LogWarning("[TimelineDirector] No stages on the timeline - nothing to walk.", this);
                phase = Phase.Finished;
                return;
            }

            // The line starts drawn only as far as the stage the player is standing on. Doing
            // this in Start rather than Awake is safe: every Awake and OnEnable has already run,
            // and nothing has rendered yet.
            if (timeline != null) timeline.GrowTo(timeline.LengthAtStop(0));

            MovePlayerTo(route[0], instant: true);
            Enter(Phase.Dwell);
        }

        // The stops on the line ARE the running order. Reading them here rather than keeping a
        // second list means the line cannot grow toward a stage the director is not going to.
        void BuildRoute()
        {
            route.Clear();
            if (timeline == null) return;
            foreach (var stop in timeline.Stops)
            {
                if (stop == null) continue;
                var place = stop.GetComponent<DancePlace>();
                if (place != null) route.Add(place);
            }
        }

        void Enter(Phase next)
        {
            phase = next;
            elapsed = 0f;
            arrived = false;

            if (next == Phase.Grow && timeline != null)
            {
                growFrom = timeline.LengthAtStop(index);
                growTo = timeline.LengthAtStop(index + 1);

                // The next stage's decoder needs a second or two to produce a first picture.
                // Starting it now, while the line is still growing, is what stops the screen
                // being blank for the first moments after arrival.
                if (index + 1 < route.Count) route[index + 1].WarmVideo();
            }
        }

        void Update()
        {
            if (phase == Phase.Finished) return;
            elapsed += Time.deltaTime;

            switch (phase)
            {
                case Phase.Dwell:
                    if (StageComplete()) Enter(Phase.Dissolve);
                    break;

                case Phase.Dissolve:
                {
                    float t = Mathf.Clamp01(elapsed / dissolveSeconds);
                    route[index].SetDissolve(t);
                    if (t >= 1f) Enter(index + 1 < route.Count ? Phase.Grow : Phase.Finished);
                    break;
                }

                case Phase.Grow:
                {
                    float t = Mathf.Clamp01(elapsed / growSeconds);
                    if (timeline != null) timeline.GrowTo(Mathf.Lerp(growFrom, growTo, t));
                    if (t >= 1f) Enter(Phase.Settle);
                    break;
                }

                case Phase.Settle:
                    if (elapsed >= settleSeconds) Enter(Phase.Travel);
                    break;

                case Phase.Travel:
                    Travel();
                    break;
            }
        }

        // The one seam that changes when the immersive video lands. Today it is a timer; then it
        // becomes "the dome's 360 video has reached its end", and nothing else in this class
        // needs to know the difference.
        bool StageComplete() => elapsed >= dwellSeconds;

        void Travel()
        {
            // Fade out, move at the darkest point, fade back in. With no fade assigned this
            // collapses to a hard cut on the first frame, which is a legible fallback rather
            // than a broken one.
            if (fade == null)
            {
                Arrive();
                return;
            }

            float half = Mathf.Max(0.0001f, fadeSeconds);

            if (!arrived)
            {
                float outward = Mathf.Clamp01(elapsed / half);
                fade.SetOpacity(outward);
                if (outward >= 1f)
                {
                    arrived = true;
                    Arrive();
                }
                return;
            }

            float back = Mathf.Clamp01((elapsed - half) / half);
            fade.SetOpacity(1f - back);
            if (back >= 1f) Enter(Phase.Dwell);
        }

        void Arrive()
        {
            index++;
            if (index >= route.Count) { phase = Phase.Finished; if (fade != null) fade.SetOpacity(0f); return; }

            // A stage the player is carried off is not a stage they walked away from. Committing
            // the pass rather than abandoning it is the difference between a result and a
            // discarded run - see DanceFollowScore. Done BEFORE the move, while the run being
            // filed still belongs to the stage that is being left.
            if (controller != null) controller.CompleteStage();

            MovePlayerTo(route[index], instant: false);

            // Without a fade there is no fade-in to wait for, so the next dwell starts here.
            // With one, Travel keeps running until the screen is clear again.
            if (fade == null) Enter(Phase.Dwell);
        }

        void MovePlayerTo(DancePlace place, bool instant)
        {
            if (origin == null || place == null) return;

            Transform anchor = place.StandingAnchor;

            // Turn FIRST, then move: rotating the rig swings the camera around the rig origin, so
            // doing it afterwards would undo the alignment we just made.
            if (matchFacing)
            {
                Vector3 forward = anchor.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 1e-6f)
                    origin.MatchOriginUpCameraForward(Vector3.up, forward.normalized);
            }

            // Align the CAMERA, not the rig origin. The player may be standing anywhere inside
            // their play space, and it is their head that has to end up on the stage.
            Vector3 target = anchor.position;
            target.y = origin.Camera != null ? origin.Camera.transform.position.y : target.y;
            origin.MoveCameraToWorldLocation(target);
        }
    }
}

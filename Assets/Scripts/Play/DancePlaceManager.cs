using UnityEngine;
using RHCommunityHack.DanceCapture;

namespace RHCommunityHack.Play
{
    // Decides which stage the player is standing on, and hands it to PlayModeController.
    //
    // Distance, not a trigger volume. A trigger box big enough to cover a stage also contains the
    // player's HANDS, and both controllers carry a Beat Hit Volume collider that is switched on
    // and off with the mode - so a mode change would fake a pair of enter/exit events. Three
    // distance comparisons a frame avoid all of that, make the hysteresis explicit, and show up
    // in the inspector as gizmos.
    //
    // MUST live on an object the stages never deactivate - same hard constraint as
    // PlayModeController, and for the same reason: a component that switches off its own object
    // can never switch itself back on.
    [DisallowMultipleComponent]
    public class DancePlaceManager : MonoBehaviour
    {
        [Tooltip("Measured from the head, not the rig origin - the player leans and steps around " +
                 "inside their play space, and it is where they actually are that matters.")]
        [SerializeField] Transform head;

        [SerializeField] DancePlace[] places = new DancePlace[0];

        [SerializeField] PlayModeController controller;
        [SerializeField] PlayModeUI ui;

        [Tooltip("The scene's single VideoPlayer, borrowed by whichever stage is occupied.")]
        [SerializeField] DanceVideoScreen videoScreen;

        public DancePlace Current { get; private set; }
        public bool OnStage => Current != null;

        void Start()
        {
            foreach (var place in places)
                if (place != null) place.SetOccupied(false);

            if (ui != null) ui.SetTarget(null);
            if (controller != null) controller.LeaveStage();
        }

        void Update()
        {
            if (head == null) return;

            DancePlace next = Resolve(head.position);
            if (next != Current) Switch(next);

            // Re-asked every frame, not once on arrival: the decoder needs about 1.7s to produce
            // a first picture, and the poster has to stay up for exactly that long. Hiding it any
            // earlier is the black rectangle this design exists to avoid.
            if (Current != null) Current.ShowLive(LiveTextureFor(Current));
        }

        DancePlace Resolve(Vector3 headPosition)
        {
            // Hysteresis: hold the stage you are on until you are past its LARGER exit radius.
            // Without this, standing on the boundary tears the stage down and rebuilds it every
            // frame - and the teardown destroys live beats, so it would look like beats randomly
            // vanishing rather than like a radius problem.
            if (Current != null && Current.SqrDistanceTo(headPosition) <= Current.ExitRadiusSqr)
                return Current;

            DancePlace best = null;
            float bestSqr = float.MaxValue;
            foreach (var place in places)
            {
                if (place == null) continue;
                float sqr = place.SqrDistanceTo(headPosition);
                if (sqr <= place.EnterRadiusSqr && sqr < bestSqr)
                {
                    best = place;
                    bestSqr = sqr;
                }
            }
            return best;
        }

        void Switch(DancePlace next)
        {
            if (Current != null)
            {
                // Order matters: tear the run down while the stage is still standing, then vacate
                // it. Vacating first would switch off the dancers and panel that teardown reads.
                if (controller != null) controller.LeaveStage();
                if (ui != null) ui.SetTarget(null);
                Current.SetOccupied(false);
            }

            Current = next;
            if (Current == null) return;

            Current.SetOccupied(true);
            if (ui != null) ui.SetTarget(Current.StatusText);
            if (controller != null)
                controller.EnterStage(Current.Take, Current.StandingAnchor, Current.Dancers);
        }

        Texture LiveTextureFor(DancePlace place)
        {
            if (videoScreen == null || place.Take == null || place.Take.video == null) return null;
            return videoScreen.IsReadyFor(place.Take.video) ? videoScreen.OutputTexture : null;
        }
    }
}

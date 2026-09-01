using System.Collections.Generic;
using UnityEngine;

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
    // Stages are FOUND, not listed. They are prefab instances now, and a list here would be one
    // more thing to remember when a fourth is dropped in - the failure being silent, since a
    // stage missing from the list simply never activates. Fill the array only to restrict the
    // scene to a subset.
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

        [Tooltip("Leave EMPTY. Every DancePlace in the scene is found at load, so dropping in " +
                 "another stage prefab needs no wiring. Fill it only to deliberately ignore " +
                 "some of the stages that are present.")]
        [SerializeField] DancePlace[] places = new DancePlace[0];

        [SerializeField] PlayModeController controller;
        [SerializeField] PlayModeUI ui;

        readonly List<DancePlace> active = new List<DancePlace>();

        public DancePlace Current { get; private set; }
        public bool OnStage => Current != null;
        public IReadOnlyList<DancePlace> Places => active;

        void Start()
        {
            Collect();

            foreach (var place in active) place.SetOccupied(false);

            // Once here as well as in Update, so the first rendered frame already has the right
            // dancers on rather than switching them in a frame later.
            if (head != null)
                foreach (var place in active) place.UpdateDancerProximity(head.position);

            if (ui != null) ui.SetTarget(null);
            if (controller != null) controller.LeaveStage();
        }

        void Collect()
        {
            active.Clear();

            if (places != null && places.Length > 0)
            {
                foreach (var place in places)
                    if (place != null) active.Add(place);
                return;
            }

            // Include inactive: a stage's own root stays active, but finding them this way also
            // survives someone parenting the stages under a group that starts switched off.
            foreach (var place in FindObjectsByType<DancePlace>(FindObjectsInactive.Include,
                                                               FindObjectsSortMode.None))
                active.Add(place);

            if (active.Count == 0)
                Debug.LogWarning("[DancePlaceManager] No DancePlace in the scene - there is " +
                                 "nowhere to dance. Drag in Assets/Prefabs/DanceStage.prefab.", this);
        }

        void Update()
        {
            if (head == null) return;
            Vector3 h = head.position;

            // EVERY stage is asked, not just the occupied one: the dancers are driven by
            // distance and are visible well before a stage is taken, so a stage you are only
            // walking past still has a say.
            foreach (var place in active)
                if (place != null) place.UpdateDancerProximity(h);

            DancePlace next = Resolve(h);
            if (next != Current) Switch(next);

            // Re-asked every frame, not once on arrival: the decoder needs a second or two to
            // produce a first picture, and the poster has to stay up for exactly that long.
            // Hiding it any earlier is the black rectangle this design exists to avoid.
            if (Current != null) Current.ShowLive(Current.LiveTexture);
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
            foreach (var place in active)
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
                // it. Vacating first would switch off the panel that teardown writes into.
                if (controller != null) controller.LeaveStage();
                if (ui != null) ui.SetTarget(null);
                Current.SetOccupied(false);
            }

            Current = next;
            if (Current == null) return;

            Current.SetOccupied(true);
            if (ui != null) ui.SetTarget(Current.StatusText);
            if (controller != null) controller.EnterStage(Current);
        }
    }
}

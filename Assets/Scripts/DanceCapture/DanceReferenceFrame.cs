using UnityEngine;

namespace RHCommunityHack.DanceCapture
{
    // The "player space" a dance is recorded into and replayed out of.
    //
    // Captured ONCE - at the moment recording starts, and again at the moment playback starts -
    // then frozen. It deliberately does not follow the player afterwards. That choice is what
    // makes head rotation a non-issue: a dancer glancing sideways mid-take cannot rotate the
    // coordinate system out from under the recording, so no smoothing or filtering is needed.
    //
    // Origin is the head position including its height, not a floor projection, so a recorded
    // pose means "this far from my head" rather than "this high off the floor" - which carries
    // across dancers of different heights far better.
    public readonly struct DanceReferenceFrame
    {
        public readonly Vector3 Origin;
        public readonly Quaternion Rotation;

        public DanceReferenceFrame(Vector3 origin, Quaternion rotation)
        {
            Origin = origin;
            Rotation = rotation;
        }

        public static DanceReferenceFrame Capture(Transform head)
        {
            Vector3 forward = head.forward;
            forward.y = 0f;

            // Looking straight up or down collapses the horizontal projection to nothing.
            // Fall back to the head's up vector, which points along the body in that pose.
            if (forward.sqrMagnitude < 1e-6f)
            {
                forward = head.up;
                forward.y = 0f;
            }
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;

            return new DanceReferenceFrame(head.position, Quaternion.LookRotation(forward.normalized, Vector3.up));
        }

        // Position always comes from the head: recorded poses mean "this far from my head", and
        // anchoring anywhere else throws away the thing that makes the data transfer between
        // dancers. Rotation is allowed to come from somewhere else, because a fixed stage wants
        // the dance facing its screen rather than facing whichever way the player arrived looking.
        //
        // A null or degenerate `facing` falls back to the head, so callers that have no stage
        // behave exactly as before.
        public static DanceReferenceFrame Capture(Transform head, Transform facing)
        {
            if (head == null || facing == null) return Capture(head);

            Vector3 forward = facing.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f) return Capture(head);

            return new DanceReferenceFrame(head.position,
                                           Quaternion.LookRotation(forward.normalized, Vector3.up));
        }

        public Vector3 InverseTransformPoint(Vector3 worldPosition)
            => Quaternion.Inverse(Rotation) * (worldPosition - Origin);

        public Quaternion InverseTransformRotation(Quaternion worldRotation)
            => Quaternion.Inverse(Rotation) * worldRotation;

        public Vector3 TransformPoint(Vector3 localPosition)
            => Origin + Rotation * localPosition;

        public Quaternion TransformRotation(Quaternion localRotation)
            => Rotation * localRotation;
    }
}

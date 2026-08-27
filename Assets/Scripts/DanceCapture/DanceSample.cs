using System;
using UnityEngine;

namespace RHCommunityHack.DanceCapture
{
    // One frame of a recorded dance. Every pose is expressed in the recording's frozen
    // DanceReferenceFrame, never in world space, so a take is replayable anywhere the player
    // happens to be standing.
    //
    // Head rotation is deliberately not stored: the reference frame already fixes body
    // orientation once at the start, and where the dancer happened to be looking mid-take is
    // not part of the choreography. Head *position* is kept because it records how much the
    // dancer travelled, which matters for judging whether a take fits a play space.
    [Serializable]
    public struct DanceSample
    {
        [Tooltip("Seconds since the start of the recording.")]
        public float time;

        public Vector3 headPosition;

        public Vector3 leftPosition;
        public Quaternion leftRotation;

        public Vector3 rightPosition;
        public Quaternion rightRotation;

        public static DanceSample Lerp(in DanceSample a, in DanceSample b, float t)
        {
            return new DanceSample
            {
                time = Mathf.Lerp(a.time, b.time, t),
                headPosition = Vector3.Lerp(a.headPosition, b.headPosition, t),
                leftPosition = Vector3.Lerp(a.leftPosition, b.leftPosition, t),
                leftRotation = Quaternion.Slerp(a.leftRotation, b.leftRotation, t),
                rightPosition = Vector3.Lerp(a.rightPosition, b.rightPosition, t),
                rightRotation = Quaternion.Slerp(a.rightRotation, b.rightRotation, t)
            };
        }
    }
}

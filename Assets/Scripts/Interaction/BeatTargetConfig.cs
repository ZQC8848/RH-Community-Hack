using UnityEngine;

namespace RHCommunityHack.Interaction
{
    [CreateAssetMenu(fileName = "BeatTargetConfig", menuName = "RH Community Hack/Beat Target Config")]
    public class BeatTargetConfig : ScriptableObject
    {
        [Header("Timing (seconds)")]
        public float ringLeadTime = 1.2f;
        public float perfectWindow = 0.08f;
        public float goodWindowEarly = 0.18f;
        public float goodWindowLate = 0.18f;

        [Header("Miss-Timeout vanish (never touched)")]
        public float missTimeoutVanishDuration = 0.25f;
        public AnimationCurve missTimeoutVanishCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Miss-Touch vanish (touched, outside Good window)")]
        public float missTouchVanishDuration = 0.3f;
        public AnimationCurve missTouchVanishCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public float missTouchGrowScale = 1.5f;

        [Header("Rules")]
        [Tooltip("Which controller(s) may hit this beat. A touch from any other hand resolves as Miss-Touch regardless of timing.")]
        public BeatHand allowedHands = BeatHand.Either;

        [Header("Geometry")]
        public float sphereRadius = 0.15f;
        public float ringStartRadius = 1f;
        public AnimationCurve ringShrinkCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [Tooltip("Hit collider radius, as a multiple of sphereRadius. >1 adds forgiveness.")]
        public float hitColliderRadiusMultiplier = 1.2f;

        [Header("Feedback (optional placeholders, safe to leave empty)")]
        public GameObject perfectVfxPrefab;
        public GameObject goodVfxPrefab;
        public AudioClip perfectSfx;
        public AudioClip goodSfx;
        public AudioClip missTouchSfx;
        public AudioClip missTimeoutSfx;
    }
}

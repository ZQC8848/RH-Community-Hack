using UnityEngine;

namespace RHCommunityHack.Environment
{
    // Makes one prop appear, hang in the air alive, and leave again.
    //
    // Scale rather than a fade. A fade needs every material on the object to be transparent, and
    // these props carry up to twenty-five materials each straight off an FBX; converting them all
    // would be a lot of churn to buy an effect that reads no better than a spring-in. Scale works
    // on anything.
    //
    // It also holds the object SWITCHED OFF until its cue. That matters more than the animation:
    // the prologue props total several million triangles between them, and an object that has not
    // been revealed yet should not be costing anything at all.
    //
    // The idle motion is not decoration either - the script is insistent that these things are
    // alive. The AR glasses "stand upright and dance to the music", the cameras "are also
    // dancing", the 5G tower "appears and starts dancing". A slow bob and turn is the cheapest
    // honest reading of that.
    [DisallowMultipleComponent]
    public class PropReveal : MonoBehaviour
    {
        [Header("Appear")]
        [Tooltip("Seconds to spring up to full size.")]
        [SerializeField, Min(0.01f)] float appearSeconds = 0.7f;

        [Tooltip("How far past full size it overshoots before settling. Zero is a plain ease.")]
        [SerializeField, Range(0f, 0.6f)] float overshoot = 0.25f;

        [Tooltip("Seconds to shrink away on dismissal.")]
        [SerializeField, Min(0.01f)] float vanishSeconds = 0.8f;

        [Header("Idle")]
        [Tooltip("The script has these objects dancing rather than sitting still. Height of the " +
                 "bob, in metres.")]
        [SerializeField, Min(0f)] float bobHeight = 0.12f;

        [Tooltip("Bob cycles per second.")]
        [SerializeField, Min(0f)] float bobSpeed = 0.6f;

        [Tooltip("Degrees per second of slow turn, so a prop shows itself from every side. Zero " +
                 "for the rigged props, which face the viewer instead - see below.")]
        [SerializeField] float spinDegreesPerSecond = 12f;

        [Header("Facing")]
        [Tooltip("Keep the prop's front turned toward the headset (yaw only - it never tilts). " +
                 "For the rigged props that dance: a dancer performs TO someone, and a turntable " +
                 "spin would have it dancing at the wall half the time. Mutually exclusive with " +
                 "the spin in practice; if both are set, facing wins.")]
        [SerializeField] bool faceViewer = false;

        [Tooltip("How fast it turns to follow, in degrees per second. Slow enough to read as a " +
                 "performer shifting their weight, not a billboard snapping.")]
        [SerializeField, Min(1f)] float turnDegreesPerSecond = 90f;

        [Tooltip("Added to the facing yaw, for a model whose authored front is not +Z.")]
        [SerializeField] float facingOffsetDegrees = 0f;

        enum State { Hidden, Appearing, Idle, Vanishing }

        State state = State.Hidden;
        Vector3 baseScale;
        Vector3 basePosition;
        float t;
        float delay;
        float phase;
        Transform viewer;

        public bool IsVisible => state == State.Appearing || state == State.Idle;

        void Awake()
        {
            baseScale = transform.localScale;
            basePosition = transform.localPosition;

            // Every prop gets its own bob offset, or a row of them pumps in unison and reads as
            // one object rather than a crowd.
            phase = Random.Range(0f, Mathf.PI * 2f);

            gameObject.SetActive(false);
        }

        // `after` staggers a group so they do not all arrive on the same frame.
        public void Appear(float after = 0f)
        {
            delay = Mathf.Max(0f, after);
            t = 0f;
            state = State.Appearing;

            // Enabled here rather than at the end of the delay: SetActive on a disabled object
            // is what starts Update running at all, and the scale is already zero so nothing is
            // visible during the wait.
            transform.localScale = Vector3.zero;
            gameObject.SetActive(true);
        }

        public void Vanish()
        {
            if (state == State.Hidden) return;
            t = 0f;
            delay = 0f;
            state = State.Vanishing;
        }

        // Straight back to the authored state with no animation, for scene setup and for a skip.
        public void HideImmediately()
        {
            state = State.Hidden;
            transform.localScale = baseScale;
            transform.localPosition = basePosition;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (delay > 0f) { delay -= Time.deltaTime; return; }

            switch (state)
            {
                case State.Appearing:
                    t += Time.deltaTime / appearSeconds;
                    transform.localScale = baseScale * Spring(Mathf.Clamp01(t));
                    // Face from the first frame, or it would arrive side-on and swing round.
                    if (faceViewer) FaceViewer(instant: t <= Time.deltaTime / appearSeconds);
                    if (t >= 1f) { transform.localScale = baseScale; state = State.Idle; t = 0f; }
                    break;

                case State.Idle:
                    Idle();
                    break;

                case State.Vanishing:
                    t += Time.deltaTime / vanishSeconds;
                    transform.localScale = baseScale * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                    if (t >= 1f) HideImmediately();
                    break;
            }
        }

        void Idle()
        {
            if (bobHeight > 0f && bobSpeed > 0f)
            {
                float y = Mathf.Sin((Time.time * bobSpeed * Mathf.PI * 2f) + phase) * bobHeight;
                transform.localPosition = basePosition + Vector3.up * y;
            }
            if (faceViewer) FaceViewer(instant: false);
            else if (!Mathf.Approximately(spinDegreesPerSecond, 0f))
                transform.Rotate(Vector3.up, spinDegreesPerSecond * Time.deltaTime, Space.Self);
        }

        // Yaw toward the headset. World up stays up: the props stand on the ground and a full
        // look-at would pitch them over when the viewer is taller or shorter than they are.
        void FaceViewer(bool instant)
        {
            if (viewer == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                viewer = cam.transform;
            }

            Vector3 to = viewer.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 1e-4f) return;

            var want = Quaternion.LookRotation(to.normalized, Vector3.up) * Quaternion.Euler(0f, facingOffsetDegrees, 0f);
            transform.rotation = instant
                ? want
                : Quaternion.RotateTowards(transform.rotation, want, turnDegreesPerSecond * Time.deltaTime);
        }

        // Ease out with a small overshoot - the difference between something arriving and
        // something being switched on.
        float Spring(float x)
        {
            if (overshoot <= 0f) return Mathf.SmoothStep(0f, 1f, x);
            float c = overshoot * 2.7f;
            float inv = x - 1f;
            return 1f + (c + 1f) * inv * inv * inv + c * inv * inv;
        }
    }
}

using UnityEngine;

namespace RHCommunityHack.Environment
{
    // A circular floor inside an inverted sphere, sized so its edge meets the sphere exactly.
    //
    // Slide it up or down and the radius follows:
    //
    //     r = sqrt(R^2 - h^2)
    //
    // where R is the dome's radius and h the height above its centre. At the centre the floor is
    // the full width of the dome; at either pole it shrinks to nothing. Anything else leaves either
    // a gap at the wall or a floor poking through it.
    //
    // Runs in edit mode so the radius updates while you drag the height - the whole point is being
    // able to place it by eye.
    //
    // Works whether it is a child of the dome or a sibling: it positions and scales itself in WORLD
    // space and divides out whatever its parent is doing. Parenting it under the dome would
    // otherwise multiply its scale by the dome's.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ImmersiveDomeFloor : MonoBehaviour
    {
        [Tooltip("The inverted sphere this floor sits in. Leave empty to use the parent. Its " +
                 "radius is read from its scale, because the sphere mesh has radius 1.")]
        [SerializeField] Transform dome;

        [Tooltip("Height above the dome's CENTRE, in metres. Negative is below. Clamped to the " +
                 "dome's radius - beyond that there is no cross-section left to stand on.")]
        [SerializeField] float heightFromCentre;

        [Tooltip("Shrinks the floor slightly so its edge does not z-fight with the dome wall. " +
                 "A centimetre is usually enough.")]
        [SerializeField, Min(0f)] float edgeInset = 0.01f;

        Transform Dome => dome != null ? dome : transform.parent;

        // Radius of the dome, taken from its scale since the mesh is a unit sphere.
        public float DomeRadius
        {
            get
            {
                var d = Dome;
                return d != null ? Mathf.Abs(d.lossyScale.x) : 0f;
            }
        }

        public float HeightFromCentre
        {
            get => heightFromCentre;
            set { heightFromCentre = value; Apply(); }
        }

        // The cross-section radius at the current height - the number this component exists for.
        public float FloorRadius
        {
            get
            {
                float r = DomeRadius;
                float h = Mathf.Clamp(heightFromCentre, -r, r);
                return Mathf.Max(0f, Mathf.Sqrt(Mathf.Max(0f, r * r - h * h)) - edgeInset);
            }
        }

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        void Update()
        {
            // Edit mode only: catches the dome being moved or rescaled, which nothing notifies us
            // about. At runtime the dome does not change, so this would be wasted work.
            if (!Application.isPlaying) Apply();
        }

        public void Apply()
        {
            var d = Dome;
            if (d == null) return;

            var s = d.lossyScale;
            if (Mathf.Abs(s.x - s.y) > 1e-3f || Mathf.Abs(s.x - s.z) > 1e-3f)
            {
                Debug.LogWarning($"[ImmersiveDomeFloor] '{d.name}' is scaled non-uniformly {s}. " +
                                 "A squashed sphere has no single cross-section radius, so the " +
                                 "floor is sized from X and will not meet the wall.", this);
            }

            float r = DomeRadius;
            float h = Mathf.Clamp(heightFromCentre, -r, r);
            if (!Mathf.Approximately(h, heightFromCentre)) heightFromCentre = h;

            transform.position = d.position + Vector3.up * h;
            transform.rotation = Quaternion.identity;

            // World-space radius regardless of who the parent is.
            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            float radius = FloorRadius;
            transform.localScale = new Vector3(
                SafeDivide(radius, parentScale.x),
                SafeDivide(1f, parentScale.y),
                SafeDivide(radius, parentScale.z));
        }

        static float SafeDivide(float value, float by) => Mathf.Approximately(by, 0f) ? value : value / by;

        void OnDrawGizmosSelected()
        {
            var d = Dome;
            if (d == null) return;

            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.8f);
            Gizmos.DrawWireSphere(d.position, DomeRadius);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(d.position, transform.position);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace RHCommunityHack.Interaction
{
    // Draws a fading line behind a moving point, for as long as something keeps holding it open.
    //
    // It knows nothing about orbs, hands or dance takes - a driver calls Track() once per frame
    // with a position and whether the gate is open, and this handles buffering, ageing and
    // rendering. That keeps it reusable and keeps the driver (GuideOrb) from growing a second
    // job on top of judging contact.
    //
    // Lives on the same GameObject as the LineRenderer it drives.
    [RequireComponent(typeof(LineRenderer))]
    public class HandTrail : MonoBehaviour
    {
        [Tooltip("How long a point survives before it is dropped. This bounds the trail's " +
                 "length - raise it to let the stroke persist longer.")]
        [SerializeField, Min(0.05f)] float pointSeconds = 0.6f;
        [Tooltip("Keep drawing for this long after the gate closes. Without it, a fast pass " +
                 "through the gate leaves a dot rather than a readable stroke.")]
        [SerializeField, Min(0f)] float graceSeconds = 1f;
        [Tooltip("Minimum movement before another point is added, so a stationary source does " +
                 "not pile hundreds of points onto one spot.")]
        [SerializeField, Min(0f)] float minDistance = 0.005f;
        [SerializeField, Min(2)] int maxPoints = 200;

        struct TrailPoint
        {
            public Vector3 position;
            public float time;
        }

        readonly List<TrailPoint> points = new List<TrailPoint>(256);
        LineRenderer line;
        Vector3 trackedPosition;
        bool hasPosition;
        float lastOpenTime = float.NegativeInfinity;

        void Awake()
        {
            line = GetComponent<LineRenderer>();
            // Points are world positions of whatever is being followed, so the renderer must not
            // treat them as local to its own transform.
            line.useWorldSpace = true;
            line.positionCount = 0;
        }

        // Call once per frame from the driver. gateOpen refreshes the grace window; the position
        // is remembered either way, so the trail can keep drawing after the gate closes.
        public void Track(Vector3 position, bool gateOpen)
        {
            trackedPosition = position;
            hasPosition = true;
            if (gateOpen) lastOpenTime = Time.time;
        }

        public void SetColor(Color color)
        {
            if (line == null) return;

            // Oldest end transparent, newest end at full colour, so the stroke fades out behind
            // the source instead of ending in a hard edge.
            line.startColor = new Color(color.r, color.g, color.b, 0f);
            line.endColor = color;
        }

        public void Clear()
        {
            points.Clear();
            lastOpenTime = float.NegativeInfinity;
            if (line != null) line.positionCount = 0;
        }

        // LateUpdate, not Update: the driver calls Track() from its own Update, and component
        // Update order is undefined. Doing the work here guarantees this frame's Track has
        // already landed.
        void LateUpdate()
        {
            if (line == null) return;

            float now = Time.time;

            if (hasPosition && now - lastOpenTime <= graceSeconds)
            {
                bool moved = points.Count == 0 ||
                    (points[points.Count - 1].position - trackedPosition).sqrMagnitude
                        >= minDistance * minDistance;

                if (moved)
                {
                    points.Add(new TrailPoint { position = trackedPosition, time = now });
                    if (points.Count > maxPoints) points.RemoveAt(0);
                }
            }

            // Age points out of the front whether or not the gate is open, so the line drains
            // away by itself once the grace period ends.
            while (points.Count > 0 && now - points[0].time > pointSeconds)
                points.RemoveAt(0);

            line.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
                line.SetPosition(i, points[i].position);
        }
    }
}

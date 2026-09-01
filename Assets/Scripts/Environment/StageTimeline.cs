using UnityEngine;

namespace RHCommunityHack.Environment
{
    // The zigzag white line on the ground that strings the dance stages together in date order.
    //
    // This is the script's own image, not decoration invented here: "we are standing at the start
    // of a ZIGZAG TIMELINE that stretches out from our feet, marked with many years". The stages
    // sit on it in chronological order, so walking the line is moving forward through history -
    // which is what the script asks for and what an equilateral triangle of stages could not say.
    //
    // The stops are an EXPLICIT ORDERED LIST, deliberately unlike DancePlaceManager, which finds
    // its stages automatically. Two reasons: chronological order cannot be derived from the scene,
    // and forgetting to add a stage here fails LOUDLY - the line visibly stops short of it - where
    // forgetting to register a stage with the manager would have failed silently.
    //
    // A LineRenderer rather than a generated mesh: the corners of a zigzag are the whole problem,
    // and numCornerVertices solves them for free. It also means the path is re-shaped by dragging
    // stages around rather than by regenerating an asset.
    //
    // ITS MATERIAL MUST BE DOUBLE-SIDED (Assets/Materials/StageTimeline.mat has _Cull = Off).
    // With TransformZ alignment the ribbon's front face ends up pointing DOWN, so an ordinary
    // back-face-culled material renders nothing at all - from above, from ground level, from
    // anywhere. It cost a while to find, because every other explanation looks the same: the
    // component reports the right point count, the right width and sane bounds, and the console
    // is clean. If the line ever goes invisible again, check _Cull before anything else.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public class StageTimeline : MonoBehaviour
    {
        [Tooltip("The stages, IN DATE ORDER - earliest first. The line is drawn through them in " +
                 "exactly this order, so reordering this array reorders history.")]
        [SerializeField] Transform[] stops = new Transform[0];

        [Tooltip("Line width in metres.")]
        [SerializeField, Min(0.01f)] float width = 2f;

        [Tooltip("Height above y=0. Must sit above the ground Plane (y=0) and above each dome's " +
                 "floor (y=0.01), but below the standing markers (y=0.02) so those still read on " +
                 "top of it. One centimetre of headroom either side is all there is.")]
        [SerializeField] float height = 0.015f;

        [Tooltip("Metres of line before the first stop and after the last, so the timeline reads " +
                 "as continuing rather than beginning and ending at a dome.")]
        [SerializeField, Min(0f)] float leadIn = 24f;
        [SerializeField, Min(0f)] float leadOut = 24f;

        [Tooltip("Rounding on each zigzag corner. Zero gives a hard mitre; the default softens " +
                 "the turn without making it look like a curve.")]
        [SerializeField, Range(0, 16)] int cornerVertices = 6;

        [Tooltip("Warn if two consecutive stops are closer together than this. The stages are " +
                 "meant to be far apart - close ones share a dome and stop reading as separate " +
                 "moments in time.")]
        [SerializeField, Min(0f)] float minSpacing = 50f;

        LineRenderer line;

        public float Width => width;

        void OnEnable() => Rebuild();
        void OnValidate() => Rebuild();

        void Update()
        {
            // Edit mode only: nothing notifies us when a stage is dragged, and the whole point is
            // that the line follows. At runtime the stages do not move, so this would be waste.
            if (!Application.isPlaying) Rebuild();
        }

        public void Rebuild()
        {
            if (line == null) line = GetComponent<LineRenderer>();
            if (line == null) return;

            int n = CountValid();
            if (n < 2) { line.positionCount = 0; return; }

            // A LineRenderer's ribbon faces its alignment axis. TransformZ plus a transform whose
            // forward is UP is what lays it flat on the ground; the default (View) would billboard
            // it toward the camera, i.e. stand it on edge.
            transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            line.useWorldSpace = true;
            line.alignment = LineAlignment.TransformZ;
            line.widthMultiplier = width;
            line.numCornerVertices = cornerVertices;
            line.numCapVertices = 0;
            line.textureMode = LineTextureMode.Tile;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            var points = new Vector3[n + 2];
            int w = 1;
            Vector3 first = Vector3.zero, second = Vector3.zero, last = Vector3.zero, secondLast = Vector3.zero;
            Vector3 previous = Vector3.zero;
            Transform previousStop = null;

            foreach (var stop in stops)
            {
                if (stop == null) continue;
                Vector3 p = stop.position;
                p.y = height;
                points[w] = p;

                if (w == 1) first = p;
                else if (w == 2) second = p;
                if (w >= 2)
                {
                    secondLast = previous;
                    float gap = Vector3.Distance(previous, p);
                    if (gap < minSpacing)
                        Debug.LogWarning($"[StageTimeline] '{previousStop.name}' and '{stop.name}' are " +
                                         $"{gap:F1}m apart, closer than the {minSpacing:F0}m these " +
                                         "stages are meant to keep.", this);
                }
                last = p;
                previous = p;
                previousStop = stop;
                w++;
            }

            // Lead-in and lead-out continue the first and last segments rather than picking a
            // direction of their own, so the tails stay on the zigzag.
            points[0] = first + Direction(second, first) * leadIn;
            points[n + 1] = last + Direction(secondLast, last) * leadOut;

            line.positionCount = points.Length;
            line.SetPositions(points);
        }

        static Vector3 Direction(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.y = 0f;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
        }

        int CountValid()
        {
            int n = 0;
            foreach (var stop in stops)
                if (stop != null) n++;
            return n;
        }
    }
}

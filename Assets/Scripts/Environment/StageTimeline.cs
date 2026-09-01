using System.Collections.Generic;
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

        [Tooltip("Height ABOVE EACH STAGE, not above world zero. Relative because an absolute " +
                 "height silently buries the line the moment anything moves vertically: the scene " +
                 "was raised to y=0.1 and a line pinned at an absolute 0.015 ended up 85mm under " +
                 "the ground, invisible, with nothing to indicate why. " +
                 "It sits BETWEEN the ground and the dome floor, deliberately: the line runs " +
                 "UNDER each stage's floor, so it shows on the open ground between stages and is " +
                 "hidden by the disc when it reaches one. Relative to the stage root: ground 0, " +
                 "THIS +0.005, dome floor +0.01, standing marker +0.02. Five millimetres either " +
                 "side is all the room there is - anything above +0.01 puts the line back on top " +
                 "of the floor.")]
        [SerializeField] float height = 0.005f;

        [Tooltip("Metres of line before the first stop and after the last, so the timeline reads " +
                 "as continuing rather than beginning and ending at a dome.")]
        [SerializeField, Min(0f)] float leadIn = 24f;
        [SerializeField, Min(0f)] float leadOut = 24f;

        [Tooltip("Extra folds inserted BETWEEN each pair of stages, so the line zigzags on its " +
                 "way rather than running dead straight from dome to dome. Zero gives one bend " +
                 "per stage and nothing else.")]
        [SerializeField, Range(0, 8)] int foldsPerLeg = 3;

        [Tooltip("How far each of those folds steps sideways, in metres, measured square to the " +
                 "leg. They alternate left and right, and the sign carries across stages so the " +
                 "whole path reads as one zigzag rather than restarting at every dome.")]
        [SerializeField, Min(0f)] float foldAmplitude = 9f;

        [Tooltip("Rounding on each zigzag corner. Zero gives a hard mitre; the default softens " +
                 "the turn without making it look like a curve.")]
        [SerializeField, Range(0, 16)] int cornerVertices = 6;

        [Tooltip("Warn if two consecutive stops are closer together than this. The stages are " +
                 "meant to be far apart - close ones share a dome and stop reading as separate " +
                 "moments in time.")]
        [SerializeField, Min(0f)] float minSpacing = 50f;

        LineRenderer line;

        // The FULL path, rebuilt only when the stages move. Growth then draws a prefix of it,
        // which is why the two are separate: recomputing folds every frame during a 3s grow
        // would be wasted work and would let the shape shift while it was being drawn.
        readonly List<Vector3> path = new List<Vector3>();
        readonly List<float> cumulative = new List<float>();   // arc length at each path point
        float grown = -1f;                                     // metres drawn, -1 = whole path

        public float Width => width;

        // The running order, read by TimelineDirector. Sharing this array rather than keeping a
        // second one is what stops the line growing toward a stage the director is not visiting.
        public IReadOnlyList<Transform> Stops => stops;

        // Total length of the drawn path, folds included. Note this is much longer than the
        // straight-line distance between stages - about 78m per 55m leg with three folds - so
        // growth speed and stage spacing are not the same number.
        public float TotalLength => cumulative.Count > 0 ? cumulative[cumulative.Count - 1] : 0f;

        // Arc length at which a given stage sits, so a caller can grow "up to stage N" without
        // knowing anything about folds. Index is into the ORDERED stops array.
        public float LengthAtStop(int stopIndex)
        {
            int seen = -1;
            for (int i = 0; i < path.Count; i++)
            {
                if (!isStop[i]) continue;
                seen++;
                if (seen == stopIndex) return cumulative[i];
            }
            return TotalLength;
        }

        readonly List<bool> isStop = new List<bool>();

        // Draw only the first `metres` of the path. Anything at or past the end draws all of it.
        public void GrowTo(float metres)
        {
            grown = metres;
            Redraw();
        }

        // Back to the whole path - the authoring default, and what edit mode always shows.
        public void ShowWholePath()
        {
            grown = -1f;
            Redraw();
        }

        void OnEnable() => Rebuild();
        void OnValidate() => Rebuild();

        void Update()
        {
            // Edit mode only: nothing notifies us when a stage is dragged, and the whole point is
            // that the line follows. At runtime the stages do not move, so this would be waste -
            // and growth redraws from the cached path without rebuilding it.
            if (!Application.isPlaying) Rebuild();
        }

        public void Rebuild()
        {
            if (line == null) line = GetComponent<LineRenderer>();
            if (line == null) return;

            int n = CountValid();
            if (n < 2) { line.positionCount = 0; return; }   // one point is not a path

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

            // Flatten the stops first: the folds need to look at a leg's two ends at once, and
            // a stop can be null anywhere in the array.
            var anchors = new List<Vector3>(n);
            var anchorNames = new List<string>(n);
            // Each stage carries its own base height, so a line through stages at different
            // heights follows them instead of cutting through the ground between.
            foreach (var stop in stops)
            {
                if (stop == null) continue;
                Vector3 p = stop.position;
                p.y = stop.position.y + height;
                anchors.Add(p);
                anchorNames.Add(stop.name);
            }

            var points = new List<Vector3>(anchors.Count * (foldsPerLeg + 1) + 2);

            // Placeholder: filled in once the first leg's direction is known.
            points.Add(Vector3.zero);

            // Alternates once per fold and KEEPS COUNTING ACROSS LEGS, so the sidestep after a
            // stage goes the opposite way to the one before it. Resetting per leg would put two
            // folds of the same sign either side of a dome, and the zigzag would stutter there.
            int side = 0;

            for (int i = 0; i < anchors.Count; i++)
            {
                points.Add(anchors[i]);
                if (i + 1 >= anchors.Count) break;

                Vector3 a = anchors[i];
                Vector3 b = anchors[i + 1];

                float gap = Vector3.Distance(a, b);
                if (gap < minSpacing)
                    Debug.LogWarning($"[StageTimeline] '{anchorNames[i]}' and '{anchorNames[i + 1]}' " +
                                     $"are {gap:F1}m apart, closer than the {minSpacing:F0}m these " +
                                     "stages are meant to keep.", this);

                Vector3 along = b - a;
                along.y = 0f;
                if (along.sqrMagnitude < 1e-6f) continue;

                // Square to the leg and flat, so a fold steps sideways rather than uphill.
                Vector3 sideways = Vector3.Cross(Vector3.up, along.normalized);

                for (int f = 1; f <= foldsPerLeg; f++)
                {
                    float t = (float)f / (foldsPerLeg + 1);
                    float sign = (side++ % 2 == 0) ? 1f : -1f;
                    // Lerp already carries the height between the two stages; the sideways step
                    // is flat, so nothing needs to re-assert y here.
                    points.Add(Vector3.Lerp(a, b, t) + sideways * (foldAmplitude * sign));
                }
            }

            // Lead-in and lead-out continue the first and last SEGMENTS - which are now folds,
            // not stages - rather than picking a direction of their own, so the tails stay on
            // the zigzag instead of shooting off square to it.
            points[0] = points[1] + Direction(points[2], points[1]) * leadIn;
            int end = points.Count - 1;
            points.Add(points[end] + Direction(points[end - 1], points[end]) * leadOut);

            path.Clear();
            path.AddRange(points);

            // Mark which points are stages rather than folds, so LengthAtStop can answer without
            // re-deriving the fold pattern. Index 0 is the lead-in and the last is the lead-out.
            isStop.Clear();
            for (int i = 0; i < path.Count; i++) isStop.Add(false);
            {
                int p = 1;
                foreach (var stop in stops)
                {
                    if (stop == null) continue;
                    isStop[p] = true;
                    p += 1 + foldsPerLeg;   // this stage, then the folds on the leg leaving it
                    if (p >= path.Count) break;
                }
            }

            cumulative.Clear();
            float run = 0f;
            cumulative.Add(0f);
            for (int i = 1; i < path.Count; i++)
            {
                run += Vector3.Distance(path[i - 1], path[i]);
                cumulative.Add(run);
            }

            Redraw();
        }

        // Writes the visible prefix of `path` into the LineRenderer. Split out from Rebuild so
        // growth is cheap: it walks a cached polyline instead of recomputing folds.
        void Redraw()
        {
            if (line == null) line = GetComponent<LineRenderer>();
            if (line == null || path.Count < 2) return;

            if (grown < 0f || grown >= TotalLength)
            {
                line.positionCount = path.Count;
                line.SetPositions(path.ToArray());
                return;
            }

            if (grown <= 0f) { line.positionCount = 0; return; }

            // Every whole segment that fits, then one partial segment to the exact distance -
            // without that last interpolated point the line would jump a fold at a time.
            int last = 0;
            while (last + 1 < path.Count && cumulative[last + 1] <= grown) last++;

            var drawn = new Vector3[last + 2];
            for (int i = 0; i <= last; i++) drawn[i] = path[i];

            float remain = grown - cumulative[last];
            float segment = cumulative[last + 1] - cumulative[last];
            float t = segment > 1e-4f ? Mathf.Clamp01(remain / segment) : 0f;
            drawn[last + 1] = Vector3.Lerp(path[last], path[last + 1], t);

            line.positionCount = drawn.Length;
            line.SetPositions(drawn);
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

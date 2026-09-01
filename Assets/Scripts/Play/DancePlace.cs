using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using RHCommunityHack.DanceCapture;

namespace RHCommunityHack.Play
{
    // One stage, and the root of the DanceStage prefab. Six of these stand 55.3m apart along a
    // zigzag timeline, in date order.
    //
    // The player no longer chooses which one to visit: TimelineDirector walks them along the
    // line, dissolving each dome behind them. The teleport beacon that used to stand on every
    // stage is gone, along with its TeleportationAnchor and collider - nothing in this prefab is
    // a teleport target any more.
    //
    // A stage owns everything local to it: its dome, its screen and its own VideoPlayer, its
    // dancers, its panel and the spot you stand on. It owns no gameplay. Orbs, beat
    // spawner, hit volumes and combo trails exist ONCE in the scene and are re-anchored to
    // whichever stage is occupied, because they are attached to the player's hands and there is
    // only one player. Copying them per stage would also mean scene references inside a prefab,
    // which Unity cannot serialise.
    //
    // WHAT VARIES BETWEEN STAGES IS THE DanceRecording AND NOTHING ELSE. The take carries the
    // video, the music, the character animation, the beat chart, the poster, the chroma-key
    // settings and the dome colour. So building a fourth stage is: drag in the prefab, move it,
    // drop a different asset in `take`. If you find yourself hand-editing anything else on an
    // instance, that setting belongs on DanceRecording instead.
    //
    // Nothing here drives anything. The manager asks it questions and tells it what to show.
    public class DancePlace : MonoBehaviour
    {
        [Header("Content")]
        [Tooltip("The take danced at this stage - the ONE thing that differs between stage " +
                 "instances. DancePlayer and DanceRecordingBeatSource must still have their " +
                 "own recording fields left empty; the take reaches them through " +
                 "PlayModeController.")]
        [SerializeField] DanceRecording take;

        [Header("Wiring (prefab-local - leave alone on instances)")]
        [Tooltip("Where the player stands and WHICH WAY THEY FACE - the 'Standing Anchor' child. " +
                 "One transform for both, because keeping a separate 'facing' object beside a " +
                 "'position' object guarantees the two disagree eventually.")]
        [SerializeField] Transform standingAnchor;

        [Tooltip("The quad. Shows the take's poster when nobody is here and while the decoder " +
                 "warms, and this stage's live video once there is a picture.")]
        [SerializeField] Renderer screenRenderer;

        [Tooltip("This stage's OWN VideoPlayer. Every stage decodes its own take, so stepping " +
                 "back onto a stage you have already visited resumes instantly instead of " +
                 "paying the decoder warm-up again - which is what a single shared player could " +
                 "never avoid, since its clip changed on every switch.")]
        [SerializeField] DanceVideoScreen videoScreen;

        [Tooltip("The inverted sphere around this stage. Its material and colour come from the " +
                 "take, so six stages can look like six different places with one asset each and " +
                 "no per-instance editing. This is also the ONLY thing that dissolves - the floor " +
                 "inside it is deliberately left alone, see SetDissolve.")]
        [SerializeField] Renderer domeRenderer;

        [Tooltip("This stage's dancers. Switched on and off by DISTANCE, not by occupancy - see " +
                 "dancerRenderRadius. Skinned characters are the real cost in this scene, not the " +
                 "video: there are eighteen of them across six stages.")]
        [SerializeField] DanceCharacterDirector dancers;

        [Tooltip("World-fixed panel root, enabled only while the player is standing here.")]
        [SerializeField] GameObject panel;

        [Tooltip("The readout PlayModeUI writes into while this stage is occupied.")]
        [SerializeField] Text statusText;

        [Header("Screen")]
        [Tooltip("Resize the screen to the video's own aspect ratio, fitted inside the size it " +
                 "was authored at. Without this, changing the take to portrait footage - which " +
                 "some of it is - stretches it across a landscape quad, and the whole point of " +
                 "swapping one asset falls over on the first video that is not 16:9.")]
        [SerializeField] bool fitScreenToVideo = true;

        [Header("Dancers")]
        [Tooltip("The dancers exist only while the player's head is within this many metres of " +
                 "the standing spot. Everything switches off together - the GameObject, so the " +
                 "SkinnedMeshRenderers, the Animators and the director's playable graph go with " +
                 "it, not just the draw call. " +
                 "DELIBERATELY LARGER than enterRadius. Tying it to occupancy would make the " +
                 "dancers appear at the same instant the panel does, six metres out, which reads " +
                 "as a pop; at this radius you see them from outside the dome and then walk in. " +
                 "With stages 55m apart, at most one stage is ever in range, so the running cost " +
                 "is the same three dancers either way.")]
        [SerializeField, Min(0f)] float dancerRenderRadius = 20f;

        [Tooltip("Extra metres to travel before they switch off again. Without a gap, standing " +
                 "on the boundary rebuilds the director's playable graph every frame.")]
        [SerializeField, Min(0.1f)] float dancerRenderHysteresis = 3f;

        [Header("Occupancy")]
        [Tooltip("Step inside this to take the stage.")]
        [SerializeField, Min(0.5f)] float enterRadius = 6f;
        [Tooltip("Leave only past THIS radius. Must exceed enterRadius, or standing on the " +
                 "boundary flickers the stage on and off every frame.")]
        [SerializeField, Min(0.5f)] float exitRadius = 8f;

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        static readonly int KeyColorId = Shader.PropertyToID("_KeyColor");
        static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int SpillId = Shader.PropertyToID("_SpillRemoval");

        MaterialPropertyBlock block;
        MaterialPropertyBlock domeBlock;
        bool dancersVisible;
        float dissolve;
        Vector3 authoredScreenScale;
        bool screenScaleCaptured;

        public DanceRecording Take => take;
        public Transform StandingAnchor => standingAnchor != null ? standingAnchor : transform;
        public DanceCharacterDirector Dancers => dancers;
        public DanceVideoScreen VideoScreen => videoScreen;
        public Text StatusText => statusText;
        public bool IsOccupied { get; private set; }

        public float EnterRadiusSqr => enterRadius * enterRadius;
        public float ExitRadiusSqr => Mathf.Max(exitRadius, enterRadius + 0.01f) * Mathf.Max(exitRadius, enterRadius + 0.01f);

        public float SqrDistanceTo(Vector3 worldPosition)
            => (worldPosition - StandingAnchor.position).sqrMagnitude;

        VideoClip Clip => take != null ? take.video : null;

        void Awake()
        {
            CaptureScreenScale();

            // Dancers off until proximity says otherwise. A scene saved with them visible would
            // otherwise show all eighteen on the first frame.
            if (dancers != null)
            {
                dancers.gameObject.SetActive(false);
                dancersVisible = false;
            }

            // Start vacated so a scene saved with panels visible does not open with six lit
            // stages and no player on any of them.
            SetOccupied(false);
        }

        void CaptureScreenScale()
        {
            if (screenScaleCaptured || screenRenderer == null) return;
            authoredScreenScale = screenRenderer.transform.localScale;
            screenScaleCaptured = true;
        }

        public void SetOccupied(bool occupied)
        {
            IsOccupied = occupied;

            if (panel != null) panel.SetActive(occupied);

            // Dancers are NOT touched here. They belong to distance, not to occupancy, and to
            // this component rather than to PlayModeController - see UpdateDancerProximity.

            if (!occupied) ParkVideo();
            ShowLive(occupied ? LiveTexture : null);
        }

        // Switches this stage's dancers on and off by distance. Called every frame by
        // DancePlaceManager, which already has the head position and is the scene's one update
        // driver; giving six DancePlaces an Update each would spread that decision out.
        public void UpdateDancerProximity(Vector3 headPosition)
        {
            if (dancers == null) return;

            float sqr = SqrDistanceTo(headPosition);
            float on = dancerRenderRadius * dancerRenderRadius;
            float off = (dancerRenderRadius + dancerRenderHysteresis) *
                        (dancerRenderRadius + dancerRenderHysteresis);
            bool want = dancersVisible ? sqr <= off : sqr <= on;

            if (want != dancersVisible || dancers.gameObject.activeSelf != want)
            {
                dancersVisible = want;
                // Activate BEFORE handing over the take: the director rebuilds its graph in
                // OnEnable, and building one on a disabled object achieves nothing.
                dancers.gameObject.SetActive(want);
            }

            // Re-asserted every frame rather than only on the transition. SetRecording returns
            // immediately when nothing has changed, and asserting it here means the dancers
            // recover on their own if anything else ever clears them.
            if (want) dancers.SetRecording(take);
        }

        // ---- video -------------------------------------------------------------------------
        //
        // The stage owns its player, so these are thin. Note there is still no Stop() anywhere:
        // parking pauses and rewinds, which is what keeps a visited stage instant to return to.

        public void WarmVideo()
        {
            if (videoScreen != null && Clip != null) videoScreen.WarmUp(Clip);
        }

        // Beat mode has nothing for the video to stay in step with, so it simply plays. Guide
        // mode only warms it - DancePlayer cues it to the take's in-point per pass.
        public void PlayVideo(bool freely)
        {
            if (videoScreen == null || Clip == null) return;
            if (freely) videoScreen.PlayFreely(Clip);
            else videoScreen.WarmUp(Clip);
        }

        public void ParkVideo()
        {
            if (videoScreen != null) videoScreen.Park();
        }

        // Null until this stage's decoder has actually produced a picture. Asked every frame,
        // because "is there a picture yet" only becomes true partway through the warm-up.
        public Texture LiveTexture
        {
            get
            {
                var clip = Clip;
                if (videoScreen == null || clip == null) return null;
                return videoScreen.IsReadyFor(clip) ? videoScreen.OutputTexture : null;
            }
        }

        // Pass the live texture to go live, or null to fall back to the take's poster.
        public void ShowLive(Texture live)
        {
            if (screenRenderer == null) return;

            Texture shown = live != null ? live : (take != null ? take.poster : null);
            screenRenderer.enabled = shown != null;
            if (shown == null) return;

            if (live != null) FitScreen();

            block ??= new MaterialPropertyBlock();
            screenRenderer.GetPropertyBlock(block);
            block.SetTexture(BaseMapId, shown);

            // From the take, because three shoots are never lit the same way - and because the
            // take is the only thing an instance is allowed to differ by. A negative threshold
            // makes the smoothstep return 1 everywhere, which is how "keying off" is expressed
            // without needing a second material.
            bool key = take != null && take.chromaKey;
            block.SetColor(KeyColorId, take != null ? take.keyColor : Color.green);
            block.SetFloat(ThresholdId, key ? take.keyThreshold : -1f);
            block.SetFloat(SmoothnessId, take != null ? take.keySmoothness : 0.06f);
            block.SetFloat(SpillId, key ? take.spillRemoval : 0f);

            screenRenderer.SetPropertyBlock(block);
        }

        // Fits the video inside the quad's authored size rather than filling it, so portrait
        // footage becomes a tall narrow screen instead of a stretched wide one.
        void FitScreen()
        {
            if (!fitScreenToVideo || screenRenderer == null || videoScreen == null) return;
            CaptureScreenScale();

            float aspect = videoScreen.ClipAspect;
            if (aspect <= 0f) return;

            float w = Mathf.Min(authoredScreenScale.x, authoredScreenScale.y * aspect);
            float h = Mathf.Min(authoredScreenScale.y, authoredScreenScale.x / aspect);
            var t = screenRenderer.transform;
            var wanted = new Vector3(w, h, authoredScreenScale.z);
            if ((t.localScale - wanted).sqrMagnitude > 1e-6f) t.localScale = wanted;
        }

        // ---- dissolve ------------------------------------------------------------------------

        // 0 leaves the dome solid, 1 removes it completely. Driven by TimelineDirector when the
        // player is finished with this stage.
        //
        // ONLY THE SPHERE DISSOLVES. The disc floor inside it is untouched and stays behind after
        // the dome has gone: it is the ground the player is standing on, and watching the floor
        // come apart under your feet is a different and much less comfortable experience in a
        // headset than watching the room open up above you. What is left is a lit disc marking
        // where the stage was, which the line runs straight across.
        //
        // It lives here rather than in the director because the dome's MaterialPropertyBlock has
        // to have exactly ONE owner: SetPropertyBlock replaces the whole block, so a second
        // component writing _Dissolve would wipe the per-take _BaseColor written by
        // ApplyDomeLook, and vice versa. Both paths go through this class and read the existing
        // block before writing it.
        public float Dissolve => dissolve;

        public void SetDissolve(float amount)
        {
            dissolve = Mathf.Clamp01(amount);
            if (domeRenderer == null) return;

            // Fully gone: switch off the sphere's RENDERER, not its GameObject. The floor is a
            // child of the dome, so deactivating the object would take the floor, its collider
            // and the ground under the player with it - and the floor is meant to stay.
            bool present = dissolve < 1f;
            if (domeRenderer.enabled != present) domeRenderer.enabled = present;
            if (!present) return;

            ApplyBlock(domeRenderer, ref domeBlock);
        }

        void ApplyBlock(Renderer r, ref MaterialPropertyBlock b)
        {
            if (r == null) return;
            b ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(b);
            b.SetFloat(DissolveId, dissolve);
            r.SetPropertyBlock(b);
        }

        // ---- dome --------------------------------------------------------------------------

        // Applied in edit mode too, so dropping a different take on an instance shows what that
        // stage will look like without entering play.
        public void ApplyDomeLook()
        {
            if (domeRenderer == null) return;

            if (take != null && take.domeMaterial != null && domeRenderer.sharedMaterial != take.domeMaterial)
                domeRenderer.sharedMaterial = take.domeMaterial;

            if (take == null)
            {
                // No take, no opinion - drop the override so the material's own colour shows,
                // rather than painting the dome whatever a fallback constant happens to be.
                domeRenderer.SetPropertyBlock(null);
                return;
            }

            // Read the block first so the dissolve amount already in it survives.
            domeBlock ??= new MaterialPropertyBlock();
            domeRenderer.GetPropertyBlock(domeBlock);
            domeBlock.SetColor(BaseColorId, take.domeColor);
            domeRenderer.SetPropertyBlock(domeBlock);
        }

        void OnEnable() => ApplyDomeLook();

        void OnValidate()
        {
            if (exitRadius <= enterRadius) exitRadius = enterRadius + 1f;
            ApplyDomeLook();
        }

        void OnDrawGizmosSelected()
        {
            Vector3 c = StandingAnchor.position;
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireSphere(c, enterRadius);
            Gizmos.color = new Color(1f, 0.6f, 0.3f, 0.6f);
            Gizmos.DrawWireSphere(c, exitRadius);
            Gizmos.color = new Color(0.8f, 0.5f, 1f, 0.5f);
            Gizmos.DrawWireSphere(c, dancerRenderRadius);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(c, StandingAnchor.forward * 2f);
        }
    }
}

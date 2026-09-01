using UnityEngine;
using UnityEngine.UI;
using RHCommunityHack.DanceCapture;

namespace RHCommunityHack.Play
{
    // One stage. Three of these stand at least 20m apart, and the player teleports between them
    // to pick which dance they want - walking IS the song select, which is why there is no menu.
    //
    // A stage owns only scenery: its take, its screen quad, its dancers, its panel, and the spot
    // you stand on. The gameplay itself - orbs, spawner, combo trails, hit volumes - exists once
    // in the scene and is re-anchored to whichever stage is occupied. See DancePlaceManager.
    //
    // Nothing here drives anything. The manager asks it questions and tells it what to show.
    public class DancePlace : MonoBehaviour
    {
        [Header("Content")]
        [Tooltip("The take danced at this stage. This is now the ONE place a take is chosen - " +
                 "PlayModeController no longer holds one, and DancePlayer / " +
                 "DanceRecordingBeatSource must still have their own recording fields left empty.")]
        [SerializeField] DanceRecording take;

        [Tooltip("Where the player stands and WHICH WAY THEY FACE. Use the TeleportationAnchor's " +
                 "own transform - keeping a second 'standing spot' beside it guarantees the two " +
                 "disagree eventually.")]
        [SerializeField] Transform standingAnchor;

        [Header("Scenery")]
        [Tooltip("The quad. Shows the poster when nobody is here and while the decoder warms, " +
                 "and the live RenderTexture once there is a picture.")]
        [SerializeField] Renderer screenRenderer;

        [Tooltip("Shown whenever this stage is not playing live video. Optional - with none, the " +
                 "screen simply stays hidden rather than showing a stale RenderTexture.")]
        [SerializeField] Texture poster;

        [Tooltip("This stage's dancers. Switched off entirely when nobody is here - nine skinned " +
                 "characters animating at once is the real cost in this scene, not the video.")]
        [SerializeField] DanceCharacterDirector dancers;

        [Header("Panel")]
        [Tooltip("World-fixed panel root, enabled only while the player is standing here.")]
        [SerializeField] GameObject panel;
        [Tooltip("The readout PlayModeUI writes into while this stage is occupied.")]
        [SerializeField] Text statusText;

        [Header("Chroma key")]
        [Tooltip("Key the green screen out of this stage's video. Off leaves the footage as it " +
                 "is, which is what a video that was already keyed to alpha wants.")]
        [SerializeField] bool chromaKey = true;
        [Tooltip("The backdrop colour to remove. Sample it from an actual frame rather than " +
                 "guessing pure green - lit cloth is never 0,255,0.")]
        [SerializeField] Color keyColor = new Color(0.05f, 0.75f, 0.12f, 1f);
        [Tooltip("Chroma distance below which a pixel is fully removed. Raise it until the " +
                 "backdrop is gone; too high starts eating the subject.")]
        [SerializeField, Range(0f, 0.5f)] float keyThreshold = 0.12f;
        [Tooltip("Width of the fade from keyed to kept. Wider softens hair and motion blur; " +
                 "narrower cuts harder.")]
        [SerializeField, Range(0.001f, 0.3f)] float keySmoothness = 0.06f;
        [Tooltip("Pulls green bounce back out of the pixels that survive, which is what stops " +
                 "the subject wearing a green rim.")]
        [SerializeField, Range(0f, 1f)] float spillRemoval = 0.7f;

        [Header("Beacon")]
        [Tooltip("The pillar you teleport to, and the landmark this stage is found by from across " +
                 "the room. Switched OFF as a whole GameObject while the player is standing here, " +
                 "which takes its renderer, its collider and its TeleportationAnchor with it in " +
                 "one move - so it is neither a pillar in your face while you dance, nor a " +
                 "teleport target for a stage you are already standing on. Put the " +
                 "TeleportationAnchor on THIS object rather than the parent: that is what lets one " +
                 "SetActive do all three jobs, and it keeps XRI out of this script.")]
        [SerializeField] GameObject beacon;

        [Header("Occupancy")]
        [Tooltip("Step inside this to take the stage.")]
        [SerializeField, Min(0.5f)] float enterRadius = 6f;
        [Tooltip("Leave only past THIS radius. Must exceed enterRadius, or standing on the " +
                 "boundary flickers the stage on and off every frame.")]
        [SerializeField, Min(0.5f)] float exitRadius = 8f;

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int KeyColorId = Shader.PropertyToID("_KeyColor");
        static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int SpillId = Shader.PropertyToID("_SpillRemoval");
        MaterialPropertyBlock block;

        public DanceRecording Take => take;
        public Transform StandingAnchor => standingAnchor != null ? standingAnchor : transform;
        public DanceCharacterDirector Dancers => dancers;
        public Text StatusText => statusText;
        public bool IsOccupied { get; private set; }

        public float EnterRadiusSqr => enterRadius * enterRadius;
        public float ExitRadiusSqr => Mathf.Max(exitRadius, enterRadius + 0.01f) * Mathf.Max(exitRadius, enterRadius + 0.01f);

        public float SqrDistanceTo(Vector3 worldPosition)
            => (worldPosition - StandingAnchor.position).sqrMagnitude;

        void Awake()
        {
            // Start vacated so a scene saved with panels visible does not open with three lit
            // stages and no player on any of them.
            SetOccupied(false);
        }

        public void SetOccupied(bool occupied)
        {
            IsOccupied = occupied;

            if (panel != null) panel.SetActive(occupied);

            // Hidden the moment the stage is taken - which is also before the player can walk
            // into it, since occupancy starts several metres out.
            if (beacon != null) beacon.SetActive(!occupied);

            if (dancers != null)
            {
                // Activate BEFORE handing over the take: the director rebuilds its graph in
                // OnEnable, and building one on a disabled object achieves nothing.
                dancers.gameObject.SetActive(occupied);
                dancers.SetRecording(occupied ? take : null);
            }

            if (!occupied) ShowLive(null);
        }

        // Pass the live texture to go live, or null to fall back to the poster. Called every
        // frame while occupied, because "is there a picture yet" only becomes true partway
        // through the decoder warm-up.
        public void ShowLive(Texture live)
        {
            if (screenRenderer == null) return;

            Texture shown = live != null ? live : poster;
            screenRenderer.enabled = shown != null;
            if (shown == null) return;

            block ??= new MaterialPropertyBlock();
            screenRenderer.GetPropertyBlock(block);
            block.SetTexture(BaseMapId, shown);

            // Per stage, because three shoots are never lit the same way. A negative threshold
            // makes the smoothstep return 1 everywhere, which is how "keying off" is expressed
            // without needing a second material.
            block.SetColor(KeyColorId, keyColor);
            block.SetFloat(ThresholdId, chromaKey ? keyThreshold : -1f);
            block.SetFloat(SmoothnessId, keySmoothness);
            block.SetFloat(SpillId, chromaKey ? spillRemoval : 0f);

            screenRenderer.SetPropertyBlock(block);
        }

        void OnValidate()
        {
            if (exitRadius <= enterRadius) exitRadius = enterRadius + 1f;
        }

        void OnDrawGizmosSelected()
        {
            Vector3 c = StandingAnchor.position;
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireSphere(c, enterRadius);
            Gizmos.color = new Color(1f, 0.6f, 0.3f, 0.6f);
            Gizmos.DrawWireSphere(c, exitRadius);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(c, StandingAnchor.forward * 2f);
        }
    }
}

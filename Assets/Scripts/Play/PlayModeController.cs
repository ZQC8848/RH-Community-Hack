using System;
using UnityEngine;
using UnityEngine.InputSystem;
using RHCommunityHack.Interaction;
using RHCommunityHack.DanceCapture;

namespace RHCommunityHack.Play
{
    // Lets the player choose between the two interactions in one scene: hitting beats, or
    // following guide orbs. Both read the same take.
    //
    // This is the integration layer, and it is the only thing that knows about both modules -
    // `Interaction/` and `DanceCapture/` still know nothing about each other.
    //
    // MUST live on a GameObject that is never deactivated by the mode switch. A component that
    // switches off the object it lives on can never switch itself back on; that trap is why the
    // capture scene needed DanceCaptureModeController in the first place.
    public class PlayModeController : MonoBehaviour
    {
        public enum Mode { Beat, Guide }

        [Header("Take")]
        [Tooltip("The ONE place the take is chosen. Pushed down to DancePlayer and to " +
                 "DanceRecordingBeatSource on every mode switch, so leave their own recording " +
                 "fields empty in this scene - assigning them as well only creates two answers " +
                 "to one question.")]
        [SerializeField] DanceRecording take;

        [Header("Beat mode")]
        [SerializeField] GameObject beatRoot;
        [SerializeField] BeatSpawner spawner;
        [SerializeField] DanceRecordingBeatSource beatSource;
        [Tooltip("Trigger volumes on the controllers. Children of the hands, so they cannot be " +
                 "grouped under beatRoot and are toggled separately.")]
        [SerializeField] GameObject[] handHitVolumes;

        [Header("Guide mode")]
        [SerializeField] GameObject guideRoot;
        [SerializeField] DancePlayer player;
        [SerializeField] GuideOrb[] orbs;

        [Header("Start")]
        [SerializeField] Mode startMode = Mode.Guide;

        [Header("Recalibrate")]
        [Tooltip("How long B must be held to re-anchor the running mode to where the player is " +
                 "standing now.")]
        [SerializeField, Range(0.2f, 10f)] float recalibrateHoldSeconds = 1f;

        public Mode CurrentMode { get; private set; }
        public DanceRecording Take => take;

        public event Action<Mode> OnModeChanged;
        public event Action OnRecalibrated;

        public float RecalibrateHoldSeconds => recalibrateHoldSeconds;

        // 0..1 while B is held, so the UI can show the hold filling up. Without it there is no
        // way to tell a hold in progress from a button that did nothing.
        public float RecalibrateProgress => holdSeconds > 0f
            ? Mathf.Clamp01(holdSeconds / recalibrateHoldSeconds)
            : 0f;

        InputAction switchAction;
        InputAction recalibrateAction;
        float holdSeconds;

        void Awake()
        {
            // Bound in code rather than in the shared XRI asset, same as the other dev-facing
            // bindings in this project. X is free here - nothing records in this scene.
            switchAction = new InputAction("SwitchPlayMode", InputActionType.Button);
            switchAction.AddBinding("<XRController>{LeftHand}/primaryButton");   // X on Meta Touch
            switchAction.AddBinding("<Keyboard>/x");

            recalibrateAction = new InputAction("RecalibratePlayOrigin", InputActionType.Button);
            recalibrateAction.AddBinding("<XRController>{RightHand}/secondaryButton");   // B on Meta Touch
            recalibrateAction.AddBinding("<Keyboard>/b");
        }

        void OnEnable()
        {
            switchAction.Enable();
            recalibrateAction.Enable();
        }

        void OnDisable()
        {
            switchAction.Disable();
            recalibrateAction.Disable();
        }

        void OnDestroy()
        {
            switchAction?.Dispose();
            recalibrateAction?.Dispose();
        }

        void Start() => SetMode(startMode);

        void Update()
        {
            if (switchAction.WasPressedThisFrame()) Toggle();
            TickRecalibrateHold();
        }

        void TickRecalibrateHold()
        {
            if (recalibrateAction.IsPressed())
            {
                holdSeconds += Time.unscaledDeltaTime;
                if (holdSeconds >= recalibrateHoldSeconds)
                {
                    holdSeconds = 0f;
                    Recalibrate();
                }
                return;
            }

            holdSeconds = 0f;
        }

        // Re-anchors whichever mode is running. Beat mode anchors inside DanceRecordingBeatSource
        // and guide mode inside DancePlayer, so the same gesture has to reach a different owner
        // depending on mode - which is why it lives here rather than in either of them.
        public void Recalibrate()
        {
            if (CurrentMode == Mode.Beat)
            {
                // Beats already in the air were placed against the OLD frame; leaving them up
                // after re-anchoring would mix two coordinate frames on screen at once.
                DestroyLiveBeats();
                if (beatSource != null) beatSource.Recalibrate();
                if (spawner != null) spawner.StartSpawning();
            }
            else if (player != null)
            {
                player.RecalibrateOrigin();
            }

            OnRecalibrated?.Invoke();
        }

        public void Toggle() => SetMode(CurrentMode == Mode.Beat ? Mode.Guide : Mode.Beat);

        public void SetMode(Mode mode)
        {
            TearDown();

            CurrentMode = mode;
            bool beat = mode == Mode.Beat;

            if (beatRoot != null) beatRoot.SetActive(beat);
            foreach (var volume in handHitVolumes)
                if (volume != null) volume.SetActive(beat);

            // DancePlayer lives UNDER guideRoot, so this switches it off too. That matters for
            // more than tidiness: DancePlayer owns its own hold-B action, and left running it
            // would re-anchor and restart playback in the middle of beat mode. Keeping it inside
            // the group means the grouping enforces that, rather than a line here that has to be
            // remembered.
            if (guideRoot != null) guideRoot.SetActive(!beat);

            if (beat) StartBeat();
            else StartGuide();

            OnModeChanged?.Invoke(mode);
        }

        // Everything here is state that outlives its owner being switched off.
        void TearDown()
        {
            DestroyLiveBeats();

            if (spawner != null) spawner.StopSpawning();
            if (player != null) player.Stop();

            // World-space particles do not disappear when their emitter is hidden - they live out
            // their lifetime wherever they were left.
            foreach (var orb in orbs)
                if (orb != null) orb.ClearTrail();
        }

        // Beats in flight keep running their own state machine and would resolve as
        // Miss-Timeout one by one after the thing that spawned them has gone.
        void DestroyLiveBeats()
        {
            foreach (var beat in FindObjectsByType<BeatTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Destroy(beat.gameObject);
        }

        void StartBeat()
        {
            if (beatSource != null)
            {
                beatSource.SetRecording(take);
                beatSource.Recalibrate();
            }

            // Explicit, because re-activating a GameObject does not re-run Start() - the spawner
            // would sit idle on every mode switch after the first.
            if (spawner != null) spawner.StartSpawning();
        }

        void StartGuide()
        {
            if (player == null) return;

            if (take != null && player.Recording != take) player.LoadRecording(take);

            // Re-anchors to where the player is standing now and restarts the pass from the top.
            player.RecalibrateOrigin();
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;

namespace RHCommunityHack.DanceCapture
{
    // Replays a DanceRecording onto two proxy transforms (the boxes you watch move).
    //
    // The playback origin is calibrated ONCE - on the first Play - and then kept. Loops do NOT
    // re-sample it, so a take stays pinned exactly where it was first anchored and successive
    // passes are directly comparable. Holding B re-anchors to where you are standing now - handled
    // here in the capture scene, but handed to PlayModeController in PlayScene, where the same
    // gesture must also re-anchor beat mode (see handleRecalibrateInput).
    public class DancePlayer : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("Assign this when DancePlayer runs on its own, as it does in the capture scene. " +
                 "In PlayScene, leave it EMPTY - PlayModeController owns the take and pushes it " +
                 "in via LoadRecording(). A value here is silently overwritten.")]
        [SerializeField] DanceRecording recording;

        [Header("Scene references")]
        [Tooltip("Sampled to calibrate the playback origin. Only read when calibrating, not every frame.")]
        [SerializeField] Transform head;
        [SerializeField] Transform leftProxy;
        [SerializeField] Transform rightProxy;

        [Header("Playback")]
        [SerializeField] bool playOnStart = true;
        [SerializeField] bool loop = true;
        [Tooltip("Seconds to wait between loops, so the restart is readable.")]
        [SerializeField] float loopPause = 0.5f;
        [SerializeField] AudioSource musicSource;
        [Tooltip("Shows the video the take was recorded against, if it had one.")]
        [SerializeField] VideoPlayer videoPlayer;
        [Tooltip("Start buffering the take's video as soon as the scene loads, so the decoder's " +
                 "startup cost is paid before anyone is waiting on it.")]
        [SerializeField] bool warmUpVideoOnLoad = true;
        [Tooltip("Rewind the video to the trim in-point at the start of every loop. Turn this " +
                 "off for a take much shorter than its video, to let the video run on unbroken " +
                 "instead of being seeked several times a minute.")]
        [SerializeField] bool restartVideoEachLoop = true;

        [Header("Origin calibration")]
        [Tooltip("Seconds B must be held to re-anchor the playback origin to the current head pose.")]
        [SerializeField, Range(0.5f, 10f)] float recalibrateHoldSeconds = 3f;
        [Tooltip("Handle the hold-B gesture here. Turn OFF when something above this owns the " +
                 "gesture - in PlayScene the mode controller does, because the same hold has to " +
                 "re-anchor beat mode too, which this component knows nothing about.")]
        [SerializeField] bool handleRecalibrateInput = true;


        // Set by whoever owns the stage the player is standing on. Null anchors to the head, as
        // the capture scene does. Runtime only - a stage supplies it, it is never authored here.
        public Transform AnchorFacing { get; set; }

        public bool IsPlaying { get; private set; }
        public DanceRecording Recording => recording;
        public float PlayheadSeconds { get; private set; }
        public float TrimmedDuration => recording != null ? recording.TrimmedDuration : 0f;
        public bool HasCalibratedOrigin { get; private set; }
        public float RecalibrateHoldSeconds => recalibrateHoldSeconds;

        // 0..1 while B is held, so the UI can show the hold filling up.
        public float RecalibrateProgress => holdSeconds > 0f
            ? Mathf.Clamp01(holdSeconds / recalibrateHoldSeconds)
            : 0f;

        // Fires at the start of EVERY pass, including each loop - Play() is not a usable signal
        // for that, since loops go through StartPass without it. Anything that must reset per
        // pass (a follow-rate statistic, a particle trail) hangs off this.
        public event Action OnPassStarted;
        public event Action OnPlaybackFinished;
        public event Action OnOriginRecalibrated;

        DanceReferenceFrame frame;
        double startDsp;
        double resumeAtDsp;
        float holdSeconds;
        bool videoStartPending;
        DanceVideoScreen screen;
        InputAction recalibrateAction;

        void Awake()
        {
            recalibrateAction = new InputAction("RecalibrateDanceOrigin", InputActionType.Button);
            recalibrateAction.AddBinding("<XRController>{RightHand}/secondaryButton");   // B on Meta Touch
            recalibrateAction.AddBinding("<Keyboard>/b");
        }

        void OnEnable() => recalibrateAction.Enable();
        void OnDisable() => recalibrateAction.Disable();
        void OnDestroy() => recalibrateAction?.Dispose();

        void Start()
        {
            // Ahead of Play(), so a take that opens with playOnStart still gets the buffering
            // started at the earliest possible moment rather than at its first pass.
            if (warmUpVideoOnLoad)
            {
                var videoScreen = EnsureScreen();
                if (videoScreen != null) videoScreen.WarmUp(recording.video);
            }

            if (playOnStart) Play();
        }

        DanceVideoScreen EnsureScreen()
        {
            if (videoPlayer == null || recording == null || recording.video == null) return null;
            if (screen == null) screen = DanceVideoScreen.For(videoPlayer);
            return screen;
        }

        public void LoadRecording(DanceRecording next)
        {
            recording = next;
            Stop();
            if (next != null) Play();
        }

        public void Play()
        {
            if (recording == null || !recording.HasSamples)
            {
                Debug.LogWarning("[DancePlayer] No recording assigned, or it has too few samples.", this);
                return;
            }

            // Calibrate only if we never have. Re-anchoring on every Play would defeat the point
            // of a stable origin; use RecalibrateOrigin() (or hold B) to move it deliberately.
            if (!HasCalibratedOrigin && !TryCalibrate()) return;

            StartPass(AudioSettings.dspTime, true);
            SetProxiesVisible(true);
        }

        public void Stop()
        {
            IsPlaying = false;
            PlayheadSeconds = 0f;
            videoStartPending = false;
            if (musicSource != null) musicSource.Stop();
            // Park rather than Stop, so the buffered decoder survives. DanceRecorder calls this
            // before every take; stopping here would make the recorder pay the warm-up again.
            if (screen != null) screen.Park();
        }

        // Re-anchors the origin to the head's current pose. Playback restarts from the top so
        // the take is seen whole from the new anchor rather than jumping mid-phrase.
        public void RecalibrateOrigin()
        {
            if (!TryCalibrate()) return;

            OnOriginRecalibrated?.Invoke();

            if (recording != null && recording.HasSamples)
            {
                StartPass(AudioSettings.dspTime, true);
                SetProxiesVisible(true);
            }
        }

        bool TryCalibrate()
        {
            if (head == null)
            {
                Debug.LogWarning("[DancePlayer] No head transform assigned to anchor playback to.", this);
                return false;
            }

            frame = DanceReferenceFrame.Capture(head, AnchorFacing);
            HasCalibratedOrigin = true;
            return true;
        }

        void StartPass(double startAt, bool restartVideo)
        {
            startDsp = startAt;
            resumeAtDsp = startAt;
            IsPlaying = true;
            PlayheadSeconds = 0f;

            // Scheduled against the same dsp clock the playhead runs on, and offset to the trim
            // in-point so audio and motion stay in step when a take has been cut.
            if (musicSource != null)
            {
                musicSource.Stop();
                if (recording.music != null)
                {
                    musicSource.clip = recording.music;
                    musicSource.time = Mathf.Clamp(recording.inPoint, 0f, Mathf.Max(0f, recording.music.length - 0.01f));
                    musicSource.PlayScheduled(startAt);
                }
            }

            // VideoPlayer cannot be scheduled the way an AudioSource can, so cue it now and
            // start it from Update once the playhead actually reaches startAt - a loop pause
            // means "now" and "when the pass begins" are not the same moment.
            //
            // Cue, never Stop/Prepare. Tearing the decoder down and re-preparing it each pass is
            // what made the picture appear frozen on frame one: this machine needs ~18s to
            // deliver a first picture, and a 12s take was resetting it long before that elapsed.
            videoStartPending = false;
            var videoScreen = EnsureScreen();
            if (videoScreen != null)
            {
                videoScreen.WarmUp(recording.video);
                if (restartVideo) videoScreen.CueTo(recording.inPoint);
                videoStartPending = true;
            }
            else if (screen != null)
            {
                screen.Park();
            }

            OnPassStarted?.Invoke();
        }

        void Update()
        {
            TickRecalibrateHold();

            if (!IsPlaying || recording == null) return;

            double now = AudioSettings.dspTime;
            if (now < resumeAtDsp) return;

            // Wait for isPrepared before starting, exactly as DanceRecorder does. Calling Play()
            // while Prepare() is still in flight leaves the player wedged: it reports isPlaying
            // but never decodes past frame 0, which looked like a frozen first frame.
            if (videoStartPending && screen != null && screen.IsReadyFor(recording.video))
            {
                videoStartPending = false;
                screen.Resume();
            }

            float elapsed = (float)(now - startDsp);
            float duration = recording.TrimmedDuration;

            if (elapsed >= duration)
            {
                if (!loop)
                {
                    IsPlaying = false;
                    if (musicSource != null) musicSource.Stop();
                    if (screen != null) screen.Park();
                    OnPlaybackFinished?.Invoke();
                    return;
                }

                // Deliberately NOT re-capturing the frame here - the origin stays put across
                // loops so every pass replays in exactly the same place.
                StartPass(now + loopPause, restartVideoEachLoop);
                SetProxiesVisible(false);
                return;
            }

            SetProxiesVisible(true);
            PlayheadSeconds = elapsed;

            if (!recording.TrySample(recording.inPoint + elapsed, out DanceSample sample)) return;

            if (leftProxy != null)
            {
                leftProxy.SetPositionAndRotation(
                    frame.TransformPoint(sample.leftPosition),
                    frame.TransformRotation(sample.leftRotation));
            }
            if (rightProxy != null)
            {
                rightProxy.SetPositionAndRotation(
                    frame.TransformPoint(sample.rightPosition),
                    frame.TransformRotation(sample.rightRotation));
            }

        }

        void TickRecalibrateHold()
        {
            if (!handleRecalibrateInput)
            {
                holdSeconds = 0f;
                return;
            }

            if (recalibrateAction.IsPressed())
            {
                holdSeconds += Time.unscaledDeltaTime;
                if (holdSeconds >= recalibrateHoldSeconds)
                {
                    holdSeconds = 0f;
                    RecalibrateOrigin();
                }
                return;
            }

            holdSeconds = 0f;
        }

        void SetProxiesVisible(bool visible)
        {
            // Hidden during the loop pause so the trail restarts cleanly instead of drawing a
            // straight line back from the end of the take to its beginning.
            if (leftProxy != null && leftProxy.gameObject.activeSelf != visible)
                leftProxy.gameObject.SetActive(visible);
            if (rightProxy != null && rightProxy.gameObject.activeSelf != visible)
                rightProxy.gameObject.SetActive(visible);
        }


    }
}

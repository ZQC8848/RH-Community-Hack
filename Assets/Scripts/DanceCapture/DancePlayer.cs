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
    // passes are directly comparable. Hold B for 3s to re-anchor to where you are standing now.
    public class DancePlayer : MonoBehaviour
    {
        [Header("Source")]
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

        [Header("Origin calibration")]
        [Tooltip("Seconds B must be held to re-anchor the playback origin to the current head pose.")]
        [SerializeField, Range(0.5f, 10f)] float recalibrateHoldSeconds = 3f;

        [Header("Path preview (optional)")]
        [Tooltip("Draws the whole trimmed path in one go when playback starts.")]
        [SerializeField] LineRenderer leftPath;
        [SerializeField] LineRenderer rightPath;
        [SerializeField, Min(2)] int pathResolution = 200;

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

        public event Action<DanceRecording> OnPlaybackStarted;
        public event Action OnPlaybackFinished;
        public event Action OnOriginRecalibrated;

        DanceReferenceFrame frame;
        double startDsp;
        double resumeAtDsp;
        float holdSeconds;
        bool videoStartPending;
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
            if (playOnStart) Play();
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

            StartPass(AudioSettings.dspTime);
            SetProxiesVisible(true);
            OnPlaybackStarted?.Invoke(recording);
        }

        public void Stop()
        {
            IsPlaying = false;
            PlayheadSeconds = 0f;
            videoStartPending = false;
            if (musicSource != null) musicSource.Stop();
            if (videoPlayer != null) videoPlayer.Stop();
            ClearPathPreview();
        }

        // Re-anchors the origin to the head's current pose. Playback restarts from the top so
        // the take is seen whole from the new anchor rather than jumping mid-phrase.
        public void RecalibrateOrigin()
        {
            if (!TryCalibrate()) return;

            OnOriginRecalibrated?.Invoke();

            if (recording != null && recording.HasSamples)
            {
                StartPass(AudioSettings.dspTime);
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

            frame = DanceReferenceFrame.Capture(head);
            HasCalibratedOrigin = true;
            return true;
        }

        void StartPass(double startAt)
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

            // VideoPlayer cannot be scheduled the way an AudioSource can, so buffer it now and
            // start it from Update once the playhead actually reaches startAt - a loop pause
            // means "now" and "when the pass begins" are not the same moment.
            videoStartPending = false;
            if (videoPlayer != null)
            {
                if (recording.video == null)
                {
                    videoPlayer.Stop();
                }
                else if (videoPlayer.clip == recording.video && videoPlayer.isPrepared)
                {
                    // Already buffered on this clip - just rewind. Tearing it down and calling
                    // Prepare() again every loop is what made the picture appear frozen on frame
                    // one: a take shorter than the video reset the decoder before it had got
                    // going, over and over.
                    videoPlayer.time = recording.inPoint;
                    videoStartPending = true;
                }
                else
                {
                    videoPlayer.Stop();
                    videoPlayer.clip = recording.video;
                    videoPlayer.time = recording.inPoint;
                    videoPlayer.Prepare();
                    videoStartPending = true;
                }
            }

            RebuildPathPreview();
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
            if (videoStartPending && videoPlayer != null && videoPlayer.isPrepared)
            {
                videoStartPending = false;
                videoPlayer.Play();
            }

            float elapsed = (float)(now - startDsp);
            float duration = recording.TrimmedDuration;

            if (elapsed >= duration)
            {
                if (!loop)
                {
                    IsPlaying = false;
                    if (musicSource != null) musicSource.Stop();
                    if (videoPlayer != null) videoPlayer.Stop();
                    OnPlaybackFinished?.Invoke();
                    return;
                }

                // Deliberately NOT re-capturing the frame here - the origin stays put across
                // loops so every pass replays in exactly the same place.
                StartPass(now + loopPause);
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

        void RebuildPathPreview()
        {
            BuildPath(leftPath, true);
            BuildPath(rightPath, false);
        }

        void BuildPath(LineRenderer line, bool leftHand)
        {
            if (line == null || recording == null || !recording.HasSamples) return;

            float duration = recording.TrimmedDuration;
            if (duration <= 0f) { line.positionCount = 0; return; }

            line.positionCount = pathResolution;
            for (int i = 0; i < pathResolution; i++)
            {
                float t = recording.inPoint + duration * i / (pathResolution - 1);
                if (!recording.TrySample(t, out DanceSample sample)) continue;
                Vector3 local = leftHand ? sample.leftPosition : sample.rightPosition;
                line.SetPosition(i, frame.TransformPoint(local));
            }
        }

        void ClearPathPreview()
        {
            if (leftPath != null) leftPath.positionCount = 0;
            if (rightPath != null) rightPath.positionCount = 0;
        }
    }
}

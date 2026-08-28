using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RHCommunityHack.DanceCapture
{
    // Captures controller motion into a DanceRecording asset. Toggled with X on the left
    // Touch controller (and X on a keyboard, so the flow can be exercised at a desk).
    //
    // Takes plain Transforms rather than XR types, so this works with any rig.
    public class DanceRecorder : MonoBehaviour
    {
        public enum State { Idle, PreparingVideo, CountingDown, Recording }

        [Header("Tracked transforms")]
        [SerializeField] Transform head;
        [SerializeField] Transform leftHand;
        [SerializeField] Transform rightHand;

        [Header("Capture")]
        [Tooltip("Seconds between pressing X and the take actually starting, so the dancer has " +
                 "time to get into position.")]
        [SerializeField, Range(0f, 10f)] float countdownSeconds = 3f;
        [Tooltip("Upper bound on samples per second. Without a cap this samples every frame, " +
                 "which in an uncapped Editor can hit 800+ Hz and bloat a 3-minute take to tens " +
                 "of megabytes. Headsets run 72-120 Hz, so anything above that is wasted data.")]
        [SerializeField, Range(30f, 144f)] float maxSampleRate = 90f;

        [Header("Music (optional)")]
        [Tooltip("Track to dance to. Leave empty to record in silence.")]
        [SerializeField] AudioClip musicClip;
        [SerializeField] AudioSource musicSource;

        [Header("Video (optional)")]
        [Tooltip("Video to dance along to, shown on the screen in the scene. Its own audio track " +
                 "plays with it - assigning both this and a Music Clip will sound like two tracks " +
                 "at once.")]
        [SerializeField] VideoClip videoClip;
        [SerializeField] VideoPlayer videoPlayer;

        [Header("Output")]
        [SerializeField] string outputFolder = "Assets/DanceRecordings";
        [SerializeField] string fileNamePrefix = "Dance";

        [Header("Coordination (optional)")]
        [Tooltip("Stopped when a take begins, so a looping preview does not play over the top " +
                 "of the music you are recording to.")]
        [SerializeField] DancePlayer playerToStop;

        public State CurrentState { get; private set; } = State.Idle;
        public bool IsRecording => CurrentState == State.Recording;
        public bool IsCountingDown => CurrentState == State.CountingDown;
        public bool IsPreparingVideo => CurrentState == State.PreparingVideo;

        // Counts down to zero during CountingDown, so the UI can show whole seconds.
        public float CountdownRemaining => IsCountingDown
            ? Mathf.Max(0f, (float)(scheduledStartDsp - AudioSettings.dspTime))
            : 0f;

        public float ElapsedSeconds => IsRecording ? (float)(AudioSettings.dspTime - scheduledStartDsp) : 0f;
        public int SampleCount => buffer.Count;
        public bool HasMusic => musicClip != null;
        public bool HasVideo => videoClip != null;
        public DanceRecording LastSaved { get; private set; }

        public event Action OnCountdownStarted;
        public event Action OnRecordingStarted;
        public event Action<DanceRecording> OnRecordingSaved;
        public event Action<string> OnRecordingFailed;

        readonly List<DanceSample> buffer = new List<DanceSample>(8192);
        DanceReferenceFrame frame;
        double scheduledStartDsp;
        double nextSampleDsp;
        InputAction toggleAction;

        void Awake()
        {
            // Bound in code rather than added to the shared XRI action asset, so this dev tool
            // cannot disturb the input map the game itself depends on.
            toggleAction = new InputAction("ToggleDanceRecording", InputActionType.Button);
            toggleAction.AddBinding("<XRController>{LeftHand}/primaryButton");   // X on Meta Touch
            toggleAction.AddBinding("<Keyboard>/x");
        }

        void OnEnable() => toggleAction.Enable();

        void OnDisable()
        {
            toggleAction.Disable();
            // Never leave a half-captured take stranded in memory on a scene change.
            if (IsRecording) StopAndSave();
            else if (IsCountingDown || IsPreparingVideo) CancelCountdown();
        }

        void OnDestroy() => toggleAction?.Dispose();

        void Update()
        {
            if (toggleAction.WasPressedThisFrame()) Toggle();

            // Hold at PreparingVideo until the decoder is genuinely ready, so the countdown and
            // the video start from the same moment.
            if (IsPreparingVideo && videoPlayer != null && videoPlayer.isPrepared) BeginCountdown();

            if (IsCountingDown && AudioSettings.dspTime >= scheduledStartDsp) BeginCapture();
            if (IsRecording) CaptureSample();
        }

        public void Toggle()
        {
            switch (CurrentState)
            {
                case State.Idle: StartCountdown(); break;
                // A second press during the countdown reads as "I didn't mean that" rather than
                // as an early start.
                case State.PreparingVideo:
                case State.CountingDown: CancelCountdown(); break;
                case State.Recording: StopAndSave(); break;
            }
        }

        public void StartCountdown()
        {
            if (CurrentState != State.Idle) return;

            // A disabled recorder has no Update to advance the countdown or take samples, so
            // starting one here would strand it mid-state until something re-enabled the
            // component and fired a stale take. DanceCaptureModeController disables this
            // component whenever a recording is loaded for review.
            if (!isActiveAndEnabled)
            {
                Fail("Recorder is disabled - clear the Recording field on Dance Player to record.");
                return;
            }

            if (head == null || leftHand == null || rightHand == null)
            {
                Fail("DanceRecorder is missing one of its head/left/right transforms.");
                return;
            }

            if (playerToStop != null) playerToStop.Stop();

            // VideoPlayer has no scheduled-start equivalent, and Play() silently waits for the
            // decoder if the clip is not buffered yet. Waiting for isPrepared BEFORE the
            // countdown - rather than during it - is what keeps the video from starting seconds
            // after the motion timeline has already begun.
            if (videoClip != null && videoPlayer != null)
            {
                videoPlayer.clip = videoClip;
                videoPlayer.time = 0d;
                videoPlayer.Prepare();
                CurrentState = State.PreparingVideo;
                return;
            }

            BeginCountdown();
        }

        void BeginCountdown()
        {
            scheduledStartDsp = AudioSettings.dspTime + countdownSeconds;

            // Scheduling the music against the very same dsp timestamp the take starts at is
            // what keeps audio and motion aligned - starting it with Play() on some later frame
            // would leave an unknown offset baked into every recording.
            if (musicClip != null && musicSource != null)
            {
                musicSource.clip = musicClip;
                musicSource.time = 0f;
                musicSource.PlayScheduled(scheduledStartDsp);
            }

            CurrentState = State.CountingDown;
            OnCountdownStarted?.Invoke();
        }

        public void CancelCountdown()
        {
            if (!IsCountingDown && !IsPreparingVideo) return;
            if (musicSource != null) musicSource.Stop();
            if (videoPlayer != null) videoPlayer.Stop();
            CurrentState = State.Idle;
        }

        void BeginCapture()
        {
            if (videoClip != null && videoPlayer != null) videoPlayer.Play();

            // The frame is snapshotted here, when the take truly starts, not when the countdown
            // was requested - by now the dancer has settled into position.
            frame = DanceReferenceFrame.Capture(head);
            buffer.Clear();
            nextSampleDsp = scheduledStartDsp;
            CurrentState = State.Recording;

            OnRecordingStarted?.Invoke();
        }

        void CaptureSample()
        {
            double now = AudioSettings.dspTime;
            if (now < nextSampleDsp) return;

            // Step forward by the interval rather than from "now", so the cadence stays even
            // instead of drifting with whatever moment each frame happens to land on.
            double interval = 1d / maxSampleRate;
            nextSampleDsp += interval;
            if (nextSampleDsp < now) nextSampleDsp = now + interval;

            buffer.Add(new DanceSample
            {
                time = (float)(now - scheduledStartDsp),
                headPosition = frame.InverseTransformPoint(head.position),
                leftPosition = frame.InverseTransformPoint(leftHand.position),
                leftRotation = frame.InverseTransformRotation(leftHand.rotation),
                rightPosition = frame.InverseTransformPoint(rightHand.position),
                rightRotation = frame.InverseTransformRotation(rightHand.rotation)
            });
        }

        public DanceRecording StopAndSave()
        {
            if (!IsRecording) return null;
            CurrentState = State.Idle;

            if (musicSource != null) musicSource.Stop();
            if (videoPlayer != null) videoPlayer.Stop();

            if (buffer.Count < 2)
            {
                Fail("Take was too short to save.");
                return null;
            }

            float duration = buffer[buffer.Count - 1].time;

#if UNITY_EDITOR
            var recording = ScriptableObject.CreateInstance<DanceRecording>();
            recording.samples = buffer.ToArray();
            recording.capturedDuration = duration;
            recording.averageSampleRate = duration > 0f ? buffer.Count / duration : 0f;
            recording.label = $"{fileNamePrefix} {DateTime.Now:yyyy-MM-dd HH:mm}";
            recording.music = musicClip;
            recording.video = videoClip;
            recording.inPoint = 0f;
            recording.outPoint = 0f;

            if (!AssetDatabase.IsValidFolder(outputFolder))
            {
                int lastSlash = outputFolder.LastIndexOf('/');
                AssetDatabase.CreateFolder(outputFolder.Substring(0, lastSlash), outputFolder.Substring(lastSlash + 1));
            }

            // Timestamped so takes never collide, and so the file name itself says when the take
            // was captured. GenerateUniqueAssetPath stays as a backstop for two takes finishing
            // inside the same second.
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string path = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{fileNamePrefix}_{stamp}.asset");
            AssetDatabase.CreateAsset(recording, path);
            AssetDatabase.SaveAssets();

            LastSaved = recording;
            OnRecordingSaved?.Invoke(recording);
            Debug.Log($"[DanceRecorder] Saved {path} - {duration:F1}s, {buffer.Count} samples " +
                      $"({recording.averageSampleRate:F0} Hz)" +
                      (musicClip != null ? $", music '{musicClip.name}'" : ", no music") +
                      (videoClip != null ? $", video '{videoClip.name}'" : ", no video"), recording);
            return recording;
#else
            // Asset creation is an editor-only API. Recording on-device would need a JSON
            // (or binary) writer into Application.persistentDataPath plus a loader for it.
            Fail("Saving recordings is only supported in the Unity Editor.");
            return null;
#endif
        }

        void Fail(string reason)
        {
            Debug.LogWarning($"[DanceRecorder] {reason}", this);
            OnRecordingFailed?.Invoke(reason);
        }
    }
}

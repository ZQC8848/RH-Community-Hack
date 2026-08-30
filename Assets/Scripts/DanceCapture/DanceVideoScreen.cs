using UnityEngine;
using UnityEngine.Video;

namespace RHCommunityHack.DanceCapture
{
    // Owns the lifecycle of the capture scene's VideoPlayer, because both the recorder and the
    // player need the same clip on the same screen and must not fight over it.
    //
    // The rule this class exists to enforce: NEVER call VideoPlayer.Stop(). On this machine the
    // decoder takes ~18 seconds to deliver its first picture (measured: isPrepared went true
    // immediately, `frame` stayed at -1 until t+18.08s, after which playback was exactly
    // realtime). Stop() throws that buffered state away, so any code that stopped and re-prepared
    // per take - or per loop of a 12s take - could never get a picture out at all. That is why
    // the screen appeared frozen on frame one.
    //
    // So: warm the decoder up once at scene load, pay the 18s while the dancer is still putting
    // the headset on, and afterwards only ever pause, seek and resume.
    //
    // Attach it yourself or let DanceVideoScreen.For() add it - no scene wiring needed.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VideoPlayer))]
    public class DanceVideoScreen : MonoBehaviour
    {
        enum Phase { Empty, Warming, Seeking, Cued, Playing }

        VideoPlayer vp;
        Renderer screenRenderer;
        Phase phase = Phase.Empty;
        VideoClip current;
        bool resumeAfterSeek;
        bool resumeAfterWarm;
        float warmStartedAt;
        float seekStartedAt;

        // A seek that never reports back would strand callers waiting on IsReady forever, and a
        // recorder stuck in PreparingVideo looks exactly like the bug this class exists to fix.
        // Better to carry on slightly out of position than to hang.
        const float SeekTimeout = 5f;

        // True once a picture has genuinely been delivered and no seek is in flight. This is the
        // gate callers should wait on - NOT isPrepared, which goes true 18 seconds before the
        // first frame appears and is therefore worthless as a readiness signal here.
        bool IsReady => phase == Phase.Cued || phase == Phase.Playing;
        bool IsWarming => phase == Phase.Warming || phase == Phase.Seeking;

        // Seconds spent warming, so the UI can say something honest during an 18-second wait.
        public float WarmingSeconds => IsWarming ? UnityEngine.Time.realtimeSinceStartup - warmStartedAt : 0f;

        public static DanceVideoScreen For(VideoPlayer player)
        {
            if (player == null) return null;
            var screen = player.GetComponent<DanceVideoScreen>();
            return screen != null ? screen : player.gameObject.AddComponent<DanceVideoScreen>();
        }

        void Awake()
        {
            vp = GetComponent<VideoPlayer>();
            // This component decides when the clip starts; playOnAwake would race it.
            vp.playOnAwake = false;
            screenRenderer = GetComponent<Renderer>();
            ApplyVisibility();
        }

        // A take with no video shows NO screen, rather than a black rectangle. The quad is drawn
        // only once a picture actually exists behind it - a RenderTexture nobody has written to
        // still holds whatever was in it last, which is precisely what made an unassigned video
        // look like a broken renderer.
        void ApplyVisibility()
        {
            if (screenRenderer != null && screenRenderer.enabled != IsReady)
                screenRenderer.enabled = IsReady;
        }

        void OnDisable()
        {
            if (vp != null) vp.seekCompleted -= HandleSeekCompleted;
        }

        // Buffers a clip and parks it at the start, ready to resume instantly. Cheap to call
        // repeatedly with the same clip - it only does work when the clip actually changes.
        public void WarmUp(VideoClip clip)
        {
            if (clip == null || vp == null) return;
            if (clip == current && phase != Phase.Empty) return;

            current = clip;
            vp.clip = clip;
            vp.seekCompleted -= HandleSeekCompleted;
            phase = Phase.Warming;
            warmStartedAt = UnityEngine.Time.realtimeSinceStartup;

            // Play() rather than Prepare() alone: on this machine Prepare() reports done while
            // the decoder has still delivered nothing, and only an actual Play seems to make it
            // produce pictures. We pause again the moment the first one lands.
            vp.Prepare();
            vp.Play();
        }

        public bool IsReadyFor(VideoClip clip) => clip != null && clip == current && IsReady;

        // Warm the clip and start it the moment a picture exists, with nothing to stay in step
        // with. This is for a mode where the video is just playing in the room; guide mode uses
        // WarmUp + CueTo instead, because there the video has to line up with the take.
        //
        // The clip is still the take's - this method takes it as an argument rather than owning
        // a field of its own, so there is no second place a video can be chosen from.
        public void PlayFreely(VideoClip clip)
        {
            if (clip == null) return;
            if (clip == current && phase == Phase.Playing) return;

            if (clip != current || phase == Phase.Empty)
            {
                resumeAfterWarm = true;
                WarmUp(clip);
                return;
            }

            Resume();
        }

        // Seeks and stays paused, so the caller can start the picture at an exact later moment.
        public void CueTo(double time)
        {
            if (vp == null || phase == Phase.Empty) return;

            vp.Pause();
            double target = Mathf.Max(0f, (float)time);
            if (System.Math.Abs(vp.time - target) < 0.03d)
            {
                phase = Phase.Cued;
                return;
            }

            resumeAfterSeek = false;
            phase = Phase.Seeking;
            seekStartedAt = UnityEngine.Time.realtimeSinceStartup;
            vp.seekCompleted -= HandleSeekCompleted;
            vp.seekCompleted += HandleSeekCompleted;
            vp.time = target;
        }

        // Plays from wherever the picture is currently cued.
        public void Resume()
        {
            if (vp == null || phase == Phase.Empty) return;

            if (phase == Phase.Seeking)
            {
                // Let the seek land first, then start - starting mid-seek is exactly the kind of
                // teardown that costs another warm-up.
                resumeAfterSeek = true;
                return;
            }

            phase = Phase.Playing;
            vp.Play();
        }

        // Pauses and rewinds to the top. Deliberately not Stop() - see the note at the top.
        public void Park()
        {
            if (vp == null || phase == Phase.Empty) return;
            CueTo(0d);
        }

        void Update()
        {
            if (vp == null) return;

            ApplyVisibility();

            if (phase == Phase.Seeking &&
                UnityEngine.Time.realtimeSinceStartup - seekStartedAt > SeekTimeout)
            {
                Debug.LogWarning($"[DanceVideoScreen] Seek did not report back within {SeekTimeout}s - " +
                                 "carrying on from wherever the picture landed.", this);
                HandleSeekCompleted(vp);
                return;
            }

            if (phase != Phase.Warming) return;

            // frame >= 0 is the only trustworthy proof that decoding is actually happening.
            if (vp.frame < 0) return;

            float waited = WarmingSeconds;
            Debug.Log($"[DanceVideoScreen] '{current.name}' ready after {waited:F1}s of buffering.", this);

            if (resumeAfterWarm)
            {
                // Already running from the warm-up, so let it run. Pausing and seeking back to
                // zero here would throw away the frames those seconds just bought.
                resumeAfterWarm = false;
                phase = Phase.Playing;
                ApplyVisibility();
                return;
            }

            vp.Pause();
            phase = Phase.Cued;
            ApplyVisibility();
            CueTo(0d);
        }

        void HandleSeekCompleted(VideoPlayer source)
        {
            vp.seekCompleted -= HandleSeekCompleted;
            if (phase != Phase.Seeking) return;

            if (resumeAfterSeek)
            {
                resumeAfterSeek = false;
                phase = Phase.Playing;
                vp.Play();
                return;
            }

            phase = Phase.Cued;
        }
    }
}

using UnityEngine;
using UnityEngine.Video;

namespace RHCommunityHack.DanceCapture
{
    // A recorded dance take: controller poses over time, in the recording's own frozen
    // reference frame (see DanceReferenceFrame).
    //
    // Trimming is NON-DESTRUCTIVE. inPoint/outPoint narrow what plays back; the captured
    // samples are never edited or thrown away, so a trim is always reversible and one capture
    // can be re-cut differently later. Do not "apply" a trim by deleting samples.
    [CreateAssetMenu(fileName = "Dance", menuName = "RH Community Hack/Dance Recording")]
    public class DanceRecording : ScriptableObject
    {
        [Header("Capture info (written by DanceRecorder)")]
        public string label;
        [Tooltip("Full length of the raw capture, in seconds.")]
        public float capturedDuration;
        [Tooltip("Average samples per second actually achieved during capture.")]
        public float averageSampleRate;

        [Tooltip("Raw captured frames. Treat as read-only once written.")]
        public DanceSample[] samples = new DanceSample[0];

        [Tooltip("Track the dancer performed to, carried through from the recorder. Null if the " +
                 "take was captured in silence - playback simply stays silent in that case.")]
        public AudioClip music;

        [Tooltip("Video the dancer followed, carried through from the recorder. Null if the take " +
                 "was captured without one. The video's own audio track plays with it.")]
        public VideoClip video;

        [Tooltip("Character animation for this take - the same performance as a skeletal clip, " +
                 "used to drive the on-stage dancers. Null if no mocap was captured alongside; " +
                 "the dancers then simply stand in their bind pose.")]
        public AnimationClip characterAnimation;

        [Header("Stage presentation")]
        // Everything below is how this take LOOKS on a stage, rather than what was captured.
        //
        // It lives here, in the capture asset, so that a stage prefab has exactly ONE field to
        // change. Give a stage a different DanceRecording and its video, music, character
        // animation, poster, keying and dome colour all change together - which is the whole
        // point of the stage being a prefab. A second "stage profile" asset would restore the
        // two-assignment-points problem that moving the take onto DancePlace was meant to end.
        //
        // The keying settings in particular belong to the footage, not to the room: they are
        // properties of how that shoot was lit, and they travel with it.

        [Tooltip("Shown on the stage screen whenever this take is not playing live - while " +
                 "nobody is standing there, and during the seconds the decoder takes to " +
                 "produce a first picture. Null leaves the screen hidden instead, which is " +
                 "better than a rectangle of whatever the RenderTexture last held.")]
        public Texture poster;

        [Tooltip("Key the green screen out of this take's video. Off leaves the footage as it " +
                 "is, which is what a video already keyed to alpha wants.")]
        public bool chromaKey = true;

        [Tooltip("The backdrop colour to remove. Sample it from an actual frame rather than " +
                 "guessing pure green - lit cloth is never 0,255,0.")]
        public Color keyColor = new Color(0.05f, 0.75f, 0.12f, 1f);

        [Tooltip("Chroma distance below which a pixel is fully removed. Raise it until the " +
                 "backdrop is gone; too high starts eating the subject. Measured on this " +
                 "project's own footage: backdrop 0.00-0.02, subject 0.21-0.23, and a grey " +
                 "floor at 0.166 - so 0.03-0.15 is the usable band.")]
        [Range(0f, 0.5f)] public float keyThreshold = 0.12f;

        [Tooltip("Width of the fade from keyed to kept. Wider softens hair and motion blur; " +
                 "narrower cuts harder.")]
        [Range(0.001f, 0.3f)] public float keySmoothness = 0.06f;

        [Tooltip("Pulls green bounce back out of the pixels that survive, which is what stops " +
                 "the subject wearing a green rim.")]
        [Range(0f, 1f)] public float spillRemoval = 0.7f;

        [Tooltip("Overrides the dome material on a stage showing this take. Null keeps whatever " +
                 "the stage prefab was built with, recoloured by domeColor below - which is usually " +
                 "enough to tell three eras apart without authoring three materials.")]
        public Material domeMaterial;

        [Tooltip("The colour of the stage's dome. This REPLACES the material's own colour rather " +
                 "than tinting it, because the domes have to reach warm and violet from a blue " +
                 "base and a multiply cannot do that. The default is ImmersiveSphere.mat's own " +
                 "blue, so a take that never touches this looks exactly like the prefab - if " +
                 "you restyle that material, change this default to match.")]
        public Color domeColor = new Color(0.15f, 0.35f, 0.55f);

        [Header("Trim (non-destructive)")]
        [Min(0f)] public float inPoint;
        [Tooltip("End of the trimmed range. Zero or less means 'play to the end of the capture'.")]
        public float outPoint;

        public bool HasSamples => samples != null && samples.Length > 1;

        public float EffectiveOutPoint => outPoint > 0f ? Mathf.Min(outPoint, capturedDuration) : capturedDuration;

        public float TrimmedDuration => Mathf.Max(0f, EffectiveOutPoint - Mathf.Clamp(inPoint, 0f, capturedDuration));

        // time is measured from the start of the RAW capture, not from inPoint.
        public bool TrySample(float time, out DanceSample result)
        {
            result = default;
            if (!HasSamples) return false;

            time = Mathf.Clamp(time, samples[0].time, samples[samples.Length - 1].time);

            int low = 0;
            int high = samples.Length - 1;
            while (high - low > 1)
            {
                int mid = (low + high) / 2;
                if (samples[mid].time <= time) low = mid;
                else high = mid;
            }

            float span = samples[high].time - samples[low].time;
            // Interpolating rather than snapping to the nearest frame is what makes playback
            // independent of the frame rate it was captured at.
            float t = span > 1e-6f ? (time - samples[low].time) / span : 0f;
            result = DanceSample.Lerp(samples[low], samples[high], t);
            return true;
        }

        void OnValidate()
        {
            if (capturedDuration > 0f)
            {
                inPoint = Mathf.Clamp(inPoint, 0f, capturedDuration);
                if (outPoint > capturedDuration) outPoint = capturedDuration;
                // A trim that ends before it begins would silently produce an empty take.
                if (outPoint > 0f && outPoint <= inPoint) outPoint = Mathf.Min(inPoint + 0.1f, capturedDuration);
            }
        }
    }
}

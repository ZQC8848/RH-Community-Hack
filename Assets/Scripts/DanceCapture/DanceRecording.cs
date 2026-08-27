using UnityEngine;

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

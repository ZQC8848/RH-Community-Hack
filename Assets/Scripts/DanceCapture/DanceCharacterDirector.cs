using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace RHCommunityHack.DanceCapture
{
    // Drives several character models from ONE animation clip, so a take can be performed on
    // stage by more than one dancer at once.
    //
    // Each dancer gets its own AnimationClipPlayable, and every one of them is given the SAME
    // time value each frame. Sync is therefore a property of there being one clock, not of three
    // players happening to stay level.
    //
    // The tidier-looking version - one clip playable shared by three AnimationPlayableOutputs -
    // does NOT work, and fails quietly: only the first output is driven and the other dancers
    // stand in bind pose with no error anywhere. Measured, not assumed. So the playables are
    // duplicated and the *time* is what is shared.
    //
    // No AnimatorController is involved. The models come in with a controller-less Animator, and
    // a Playables graph plays the clip straight into it - which is also why a single clip can be
    // routed to any number of dancers without authoring a state machine per character.
    //
    // The clip comes from the take, and the take is chosen in exactly one place
    // (PlayModeController), which pushes it in via SetRecording. Do not add a take field here as
    // well - see the note on that in the PlayScene spec.
    [DisallowMultipleComponent]
    public class DanceCharacterDirector : MonoBehaviour
    {
        [Tooltip("The dancers to drive. Every one must share the rig the clip was authored " +
                 "against - these are instances of the same model, so they do.")]
        [SerializeField] Animator[] dancers = new Animator[0];

        [Tooltip("Restart the clip when it runs out. The character clip is usually shorter than " +
                 "the take it belongs to, so without this the dancers freeze partway through.")]
        [SerializeField] bool loop = true;

        DanceRecording recording;
        PlayableGraph graph;
        AnimationClipPlayable[] clipPlayables = new AnimationClipPlayable[0];
        AnimationClip clip;
        double elapsed;

        public bool IsDriving => graph.IsValid() && clip != null;
        public AnimationClip Clip => clip;
        public double PlayheadSeconds => elapsed;
        public int DancerCount => dancers != null ? dancers.Length : 0;

        // Called by whoever owns the take. Cheap to call repeatedly with the same recording.
        public void SetRecording(DanceRecording next)
        {
            if (recording == next && graph.IsValid()) return;
            recording = next;
            Rebuild();
        }

        public void Restart() => elapsed = 0d;

        void OnEnable()
        {
            if (recording != null) Rebuild();
        }

        // A PlayableGraph is not garbage collected - it must be destroyed by hand, or it leaks
        // and keeps writing to the Animators it was bound to.
        void OnDisable() => Teardown();
        void OnDestroy() => Teardown();

        void Rebuild()
        {
            Teardown();

            clip = recording != null ? recording.characterAnimation : null;
            if (clip == null) return;

            graph = PlayableGraph.Create("DanceCharacters");

            // Manual: the time is pushed in from one place in Update below. Letting the graph
            // advance its own clock as well would leave two things deciding where we are.
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            var built = new System.Collections.Generic.List<AnimationClipPlayable>(dancers.Length);
            for (int i = 0; i < dancers.Length; i++)
            {
                var dancer = dancers[i];
                if (dancer == null) continue;

                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false);

                var output = AnimationPlayableOutput.Create(graph, "Dancer " + i, dancer);
                output.SetSourcePlayable(playable);
                built.Add(playable);
            }
            clipPlayables = built.ToArray();
            int bound = clipPlayables.Length;

            if (bound == 0)
            {
                Debug.LogWarning($"[DanceCharacterDirector] '{clip.name}' has no dancers to drive.", this);
                Teardown();
                return;
            }

            elapsed = 0d;
            graph.Play();
            graph.Evaluate();   // Pose them on frame one rather than leaving a bind-pose flash.
        }

        void Update()
        {
            if (!graph.IsValid() || clip == null) return;

            elapsed += UnityEngine.Time.deltaTime;

            double length = clip.length;
            double t = elapsed;
            if (length > 0d)
            {
                if (loop) t = elapsed % length;
                else if (t > length) t = length;
            }

            // One time value into every playable - this is the whole synchronisation mechanism.
            for (int i = 0; i < clipPlayables.Length; i++) clipPlayables[i].SetTime(t);
            graph.Evaluate();
        }

        void Teardown()
        {
            if (graph.IsValid()) graph.Destroy();
            clipPlayables = new AnimationClipPlayable[0];
            clip = null;
        }
    }
}

using UnityEngine;
using RHCommunityHack.Environment;

namespace RHCommunityHack.Play
{
    // One beat of the prologue: a line of narration, and whatever appears while it is spoken.
    //
    // The prologue is the black space the script opens in, before the ground timeline counts back
    // to 1964: "objects and logos appear around us... each digital asset shows its name and year".
    // Every one of those appearances is tied to a spoken line, so a cue is the natural unit and
    // the whole prologue is an array of them in the inspector.
    //
    // THE VOICE CLIP IS THE SEAM. It is null today because the narration has not been recorded.
    // When it arrives, drop it in and the cue starts waiting for the audio to finish instead of
    // for `seconds` - see PrologueDirector.CueComplete, which is the only code that has to know.
    // Nothing else in the sequence changes, and a half-recorded prologue works: cues with a clip
    // wait for it, cues without fall back to their timer.
    [System.Serializable]
    public class PrologueCue
    {
        [Tooltip("Inspector label only - never shown to the player. Name it after the beat, e.g. " +
                 "'immersive cameras', so a twenty-entry array stays readable.")]
        public string label;

        [Tooltip("Who is speaking. Shown above the subtitle; leave empty for no attribution.")]
        public string speaker = "NARRATOR";

        [Tooltip("The subtitle. Blank shows nothing, which is how a silent beat - objects sliding " +
                 "away, a pause - is written.")]
        [TextArea(2, 6)] public string subtitle;

        [Tooltip("RESERVED for the recorded narration. Null today. With a clip assigned the cue " +
                 "lasts as long as the clip plus holdAfter; without one it lasts `seconds`.")]
        public AudioClip voice;

        [Tooltip("How long this cue lasts when it has no voice clip. Ignored once one is assigned.")]
        [Min(0f)] public float seconds = 5f;

        [Tooltip("Extra beat after the line ends before the next cue starts, so the narration does " +
                 "not run on top of itself.")]
        [Min(0f)] public float holdAfter = 0.6f;

        [Tooltip("What appears on this cue. They are switched off until then - which is also what " +
                 "keeps these props off the frame budget for the rest of the piece.")]
        public PropReveal[] reveal = new PropReveal[0];

        [Tooltip("Seconds between each object in `reveal`. Zero pops the whole group at once; the " +
                 "default lets a rank of AR glasses arrive one after another the way the script " +
                 "describes them.")]
        [Min(0f)] public float revealStagger = 0.25f;

        [Tooltip("Send everything revealed so far away again. The script's last prologue beat: " +
                 "'the objects slide slowly into the distance, and the ground timeline counts back " +
                 "to 1964'.")]
        public bool dismissAll;
    }
}

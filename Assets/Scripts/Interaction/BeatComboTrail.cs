using System;
using UnityEngine;

namespace RHCommunityHack.Interaction
{
    // Draws a trail behind the player's hands during beat mode, in the same style guide mode uses,
    // but earned rather than given: the trail grows and brightens as beats are hit well, and
    // shrinks back on misses.
    //
    //   Perfect  +2      Good  +1      Miss-Touch / Miss-Timeout  -1
    //
    // At level 0 there is no trail at all; at maxLevel it is at full length and full colour.
    //
    // Everything here lives in the interaction module - no dependency on the capture system, so
    // this travels with the beat loop.
    public class BeatComboTrail : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("Subscribed to for every beat it spawns, so each beat's judgment can be heard.")]
        [SerializeField] BeatSpawner spawner;

        [Header("Hands")]
        [SerializeField] Transform leftHand;
        [SerializeField] Transform rightHand;
        [SerializeField] HandTrail leftTrail;
        [SerializeField] HandTrail rightTrail;

        [Header("Colour")]
        [Tooltip("Matches the beat flavour for that hand - magenta is the left-hand beat, cyan " +
                 "the right - so the trail reads as belonging to the beats being hit.")]
        [SerializeField] Color leftColor = new Color(1f, 0.38f, 0.9f, 1f);
        [SerializeField] Color rightColor = new Color(0.35f, 0.95f, 1f, 1f);
        [Tooltip("How far level 1 is pushed toward grey. The colour walks from there to the full " +
                 "flavour colour as the level climbs, so vividness reads as progress.")]
        [SerializeField, Range(0f, 1f)] float lowLevelDullness = 0.85f;

        [Header("Level")]
        [SerializeField, Range(1, 10)] int maxLevel = 5;
        [SerializeField] int perfectGain = 2;
        [SerializeField] int goodGain = 1;
        [SerializeField] int missPenalty = 1;
        [Tooltip("Trail length at max level, in seconds of hand movement.")]
        [SerializeField, Min(0.05f)] float maxTrailSeconds = 0.8f;

        public int Level { get; private set; }
        public int MaxLevel => maxLevel;
        public float Fill01 => maxLevel > 0 ? (float)Level / maxLevel : 0f;

        public event Action<int> OnLevelChanged;

        void OnEnable()
        {
            if (spawner != null) spawner.OnBeatSpawned += HandleBeatSpawned;

            // Beat mode is entered fresh each time - carrying a level across a mode switch would
            // hand the player a trail they did not earn in this run.
            Level = 0;
            ApplyLevel();
        }

        void OnDisable()
        {
            if (spawner != null) spawner.OnBeatSpawned -= HandleBeatSpawned;
        }

        void HandleBeatSpawned(BeatTarget beat)
        {
            if (beat != null) beat.OnResolved += HandleResolved;
        }

        void HandleResolved(BeatTarget beat, JudgmentResult result)
        {
            beat.OnResolved -= HandleResolved;

            int delta;
            switch (result)
            {
                case JudgmentResult.Perfect: delta = perfectGain; break;
                case JudgmentResult.Good: delta = goodGain; break;
                // Both failures cost the same: a mistimed or wrong-handed touch and a beat left
                // to expire are equally "not hit".
                default: delta = -missPenalty; break;
            }

            SetLevel(Level + delta);
        }

        public void SetLevel(int next)
        {
            next = Mathf.Clamp(next, 0, maxLevel);
            if (next == Level) return;

            bool droppedToZero = next == 0;
            Level = next;
            ApplyLevel();

            // Level zero means gone, not merely short: without clearing, HandTrail's grace period
            // would keep the last stroke drawing for another second after the trail was lost.
            if (droppedToZero)
            {
                if (leftTrail != null) leftTrail.Clear();
                if (rightTrail != null) rightTrail.Clear();
            }

            OnLevelChanged?.Invoke(Level);
        }

        void ApplyLevel()
        {
            float fill = Fill01;
            Apply(leftTrail, leftColor, fill);
            Apply(rightTrail, rightColor, fill);
        }

        void Apply(HandTrail trail, Color color, float fill)
        {
            if (trail == null) return;
            trail.SetLength(maxTrailSeconds * fill);
            trail.SetColor(Color.Lerp(Dull(color), color, fill));
        }

        // Toward grey and darker at once - desaturating alone leaves a pale line that still reads
        // as bright, which would blunt the difference between level 1 and level 5.
        Color Dull(Color c)
        {
            float grey = c.grayscale * (1f - lowLevelDullness * 0.5f);
            return Color.Lerp(c, new Color(grey, grey, grey, c.a), lowLevelDullness);
        }

        void Update()
        {
            // Gate closed at level 0, so nothing is drawn until the first beat is hit well.
            bool open = Level > 0;

            if (leftTrail != null && leftHand != null) leftTrail.Track(leftHand.position, open);
            if (rightTrail != null && rightHand != null) rightTrail.Track(rightHand.position, open);
        }
    }
}

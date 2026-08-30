using System.Collections.Generic;
using UnityEngine;
using RHCommunityHack.Interaction;

namespace RHCommunityHack.DanceCapture
{
    // Positions beats where a recorded dancer's hands actually were, instead of at random points.
    // The spawn CADENCE still comes from BeatSpawner's timer - only the positions come from here.
    //
    // Lives on the DanceCapture side of the boundary on purpose: it is the only piece that knows
    // both what a DanceRecording is and what a beat is, which keeps `Interaction/` free of any
    // dependency on the capture system.
    public class DanceRecordingBeatSource : BeatPlacementSource
    {
        [Header("Source")]
        [SerializeField] DanceRecording recording;

        [Header("Anchoring")]
        [Tooltip("Sampled ONCE to anchor the take, the same way DancePlayer does it. Recorded " +
                 "positions are relative to the dancer's head, so anchoring to the player's head " +
                 "puts the beats where that dancer's hands were relative to them.")]
        [SerializeField] Transform head;

        [Header("Timeline")]
        [Tooltip("Restart the take from its in-point when it runs out, so the chart keeps going.")]
        [SerializeField] bool loop = true;

        [Header("Hands")]
        [SerializeField] bool placeLeft = true;
        [SerializeField] bool placeRight = true;

        public bool HasCalibrated { get; private set; }
        public float RecordingSeconds { get; private set; }

        DanceReferenceFrame frame;
        double startDsp;
        bool started;

        public override void GetPlacements(double perfectTimeDsp, List<BeatPlacement> into)
        {
            if (recording == null || !recording.HasSamples)
            {
                Debug.LogWarning("[DanceRecordingBeatSource] No recording assigned, or it has too " +
                                 "few samples - no beats will be placed.", this);
                return;
            }

            // Calibrated lazily on the first tick rather than in Start(): an XR head pose is not
            // valid until the rig has had a frame to update, so anchoring in Start() would pin
            // the whole chart to whatever the camera's placeholder pose happened to be.
            if (!HasCalibrated)
            {
                if (head == null)
                {
                    Debug.LogWarning("[DanceRecordingBeatSource] No head transform assigned to " +
                                     "anchor the take to.", this);
                    return;
                }

                frame = DanceReferenceFrame.Capture(head);
                HasCalibrated = true;
            }

            if (!started)
            {
                // Recording time zero lines up with the first beat's HIT moment, so the chart
                // starts at the top of the take no matter what start delay the spawner uses.
                startDsp = perfectTimeDsp;
                started = true;
            }

            float duration = recording.TrimmedDuration;
            if (duration <= 0f) return;

            float elapsed = (float)(perfectTimeDsp - startDsp);
            if (loop) elapsed = Mathf.Repeat(elapsed, duration);
            else if (elapsed > duration) return;   // take exhausted, stop placing

            RecordingSeconds = elapsed;

            if (!recording.TrySample(recording.inPoint + elapsed, out DanceSample sample)) return;

            if (placeLeft)
            {
                into.Add(new BeatPlacement
                {
                    position = frame.TransformPoint(sample.leftPosition),
                    hand = BeatHand.Left
                });
            }

            if (placeRight)
            {
                into.Add(new BeatPlacement
                {
                    position = frame.TransformPoint(sample.rightPosition),
                    hand = BeatHand.Right
                });
            }
        }

        // Re-anchors to the head's current pose and restarts the take from its in-point.
        public void Recalibrate()
        {
            HasCalibrated = false;
            started = false;
        }

        // So one owner can point both this and DancePlayer at the same take, rather than each
        // holding its own reference that can silently drift out of step.
        public void SetRecording(DanceRecording next)
        {
            if (recording == next) return;
            recording = next;
            Recalibrate();
        }
    }
}

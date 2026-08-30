using System.Collections.Generic;
using UnityEngine;

namespace RHCommunityHack.Interaction
{
    // Where a beat should appear, and which hand it is meant for.
    public struct BeatPlacement
    {
        public Vector3 position;

        // BeatHand.Either means "this source does not care" - the spawner then picks a flavour
        // itself. A source that knows the hand (a recorded take does) names it, and the spawner
        // uses the flavour whose config accepts that hand.
        public BeatHand hand;
    }

    // Supplies the positions a spawner should place beats at.
    //
    // An abstract MonoBehaviour rather than an interface so it serialises as a plain component
    // reference in the inspector.
    //
    // This abstraction exists to keep the interaction module portable: beats can be positioned
    // from a recorded dance take, but `Interaction/` must not depend on the capture system, so
    // the adapter that reads a DanceRecording lives on the DanceCapture side of the boundary.
    // Same split as GuideOrb / DanceFollowScore - see
    // .ai/decisions/modular-portable-interaction.md
    public abstract class BeatPlacementSource : MonoBehaviour
    {
        // Fills `into` with the placements for one spawn tick. Add nothing to skip the tick.
        //
        // NOTE the parameter is the moment the beat should be HIT, not the moment it spawns.
        // A beat is telegraphed for its config's lead time before it can be hit, so a source
        // reading a performance must answer "where is the hand at that later moment" - sampling
        // at spawn time instead would put every beat where the hand was a lead-time ago, which
        // offsets the whole chart against the dance it came from.
        public abstract void GetPlacements(double perfectTimeDsp, List<BeatPlacement> into);
    }
}

using System;

namespace RHCommunityHack.Interaction
{
    // Flags rather than a plain enum so one type covers both "which hand touched" (a single
    // flag) and "which hands this beat accepts" (possibly both), instead of two parallel enums
    // that could drift apart.
    [Flags]
    public enum BeatHand
    {
        None = 0,
        Left = 1 << 0,
        Right = 1 << 1,
        Either = Left | Right
    }
}

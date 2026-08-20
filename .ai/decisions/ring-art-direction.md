# Ring renders as a billboard, and the art direction is "neon energy pulse" (not toon-candy or particle-native)

**2026-08-20 ・ status: standing ・ scope: covers only the ring/sphere's rendering approach and shader style, not the judgment logic or gameplay variables (see [../Docs/Ring-Sphere 交互判定与美术规格.md](../Docs/Ring-Sphere%20交互判定与美术规格.md))**

## What forced the choice

Two open questions from designing the ring/sphere hit mechanic needed an answer before
any shader work could start: (1) whether the shrinking "ring" telegraph is a real 3D
object (a torus mesh around the sphere) or a flat billboard always facing the player,
and (2) what visual/shader style fits a VR party game, given no art direction existed
yet.

## Decision

1. **Ring renders as a billboard** — a flat disc/ring quad that always faces the player
   camera — not a real 3D torus.
2. **Art direction is "neon energy pulse"**: sphere uses Fresnel rim glow + emission
   (reads as a glowing orb from any viewing angle); ring uses a radial-gradient glowing
   circle with additive blending; judgment tiers are color-coded via emission color
   (Perfect = gold/white, Good = blue, timeout-Miss = dim/desaturated).

## Why not the alternatives

| Alternative | Its case | Why not |
|---|---|---|
| Real 3D torus ring | Looks more "physically real," could hold up better from extreme viewing angles | In VR, judging "two concentric circles are the same size" by eye is a depth-perception task that gets *harder* in true 3D as the ring closes in — a torus viewed off-axis doesn't read as cleanly as a flat circle. Billboard removes the depth-judgment ambiguity entirely |
| Toon/candy style (glossy bubble sphere, dashed ring, "pop" on hit) | More distinctive personality, reads as more clearly "playful party game" and less "esports" | More production cost (toon ramp + iridescence + squash/pop animation) for a first playable version, before the core loop has been feel-tested at all — picking the more distinctive style before the mechanic itself is validated risks reworking shaders after gameplay tuning changes things |
| Particle-native ring (VFX Graph particles instead of a mesh/shader ring; ring particles become the hit-burst particles) | Best visual cohesion between approach and hit, highest "wow" ceiling | Requires VFX Graph work (new tooling for this project) and particle lifecycle tuning stacked on top of an already-undecided judgment state machine — too much new surface area for a first version |

## What this rests on

| Assumption | Status | Evidence, or the check that would settle it |
|---|---|---|
| Fresnel rim glow reads clearly across the range of head angles a player actually uses while reaching for a sphere in VR | unverified | Check once the first shader prototype is in a headset — verify the sphere doesn't go dark/unreadable at glancing angles |
| A billboard ring doesn't read as visually flat/cheap against a real 3D VR scene | unverified | Playtest reaction once the first prototype exists. If it reads as cheap, the toon direction's dashed-ring style might billboard better aesthetically — worth keeping as a fallback, not a full alternative |

## Accepted costs

Gives up the more distinctive "party game personality" of the toon direction for now, in
exchange for lower shader/production cost and a design that's easier to iterate on
before the core mechanic (judgment windows, timing feel) is even validated. A visual
polish pass can revisit style later without touching the billboard-vs-3D call, since that
one is closer to a UX finding than an aesthetic preference.

## What would reverse this

- Playtesting shows the neon-glow style doesn't differentiate the game enough, or
  doesn't fit the party-game identity once other art (environment, UI) comes together
- The billboard ring reads as visually flat/cheap once seen in-headset with real
  lighting

---

## When this is superseded

*(not yet superseded)*

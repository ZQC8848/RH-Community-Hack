# The approach ring's "perfect" radius is nowhere near the sphere's actual radius

**2026-08-20 ・ cost: found visually during playtest ・ resolved by: taking the sphere's scale off the shared root**

## Symptom

- At the moment the shrinking ring is supposed to exactly meet the sphere's surface, it
  was instead far *inside* the sphere — visibly wrong, not a subtle few-percent drift.
- **No error, no warning, nothing in the console.** Every number in `BeatTargetConfig`
  read correctly (`sphereRadius = 0.15`, `ringStartRadius = 1.0`), and the shrink code
  was doing exactly what it looked like it was doing: `Mathf.Lerp(ringStartRadius,
  sphereRadius, curveT)` converging on `sphereRadius`. The logic was right and the
  result was still wrong.

## Why it was hard

| Ruled out | On what evidence |
|---|---|
| Wrong shrink math / wrong curve | `TickRingScale` provably converges on `config.sphereRadius`; the endpoint value it computed was correct |
| Config values mistuned | Read straight off the asset: `sphereRadius = 0.15`, `ringStartRadius = 1.0`, both as intended |
| Shader `_RingRadius` (0.88) being the culprit | Real, but only a 12% shrink — nowhere near enough to explain a ~4x error, so it was a second bug hiding behind the first |

## What was actually true

The `Ring` was a **child of the root that also carried the sphere's scale**. `Initialize`
set the root to `sphereRadius * 2` (= 0.3) to size the sphere, so every scale the ring set
on itself got multiplied by 0.3 on the way to world space.

At the perfect moment, with `sphereRadius = 0.15` and shader `_RingRadius = 0.88`:

```
ring localScale        = sphereRadius * 2         = 0.3
ring lossyScale        = 0.3 (root) * 0.3         = 0.09
ring visible radius    = 0.09 / 2 * 0.88          = 0.0396
sphere world radius                               = 0.15
                                          -> ring was ~3.8x too small
```

## Two-minute path next time

| Instrument | Reading that means this |
|---|---|
| `transform.lossyScale` vs `transform.localScale` on the child | They differ => an ancestor is scaling you, and any radius/size you set locally is not the size you get |
| Root's `lossyScale.x` | Should be `1` for `BeatTarget`. Anything else means someone reintroduced scale on the root and every child dimension is now multiplied by it |

## Lesson

If a GameObject sets its own size to mean something in world units, do not parent it under
a transform that is itself scaled to size a sibling. Give the root scale 1 and let each
visual child own its scale — compensating for parent scale in code works but leaves the
trap armed for the next person. Related: when code and a shader both need the same
constant (here `_RingRadius`), read it from the material at runtime instead of hard-coding
it in both places.

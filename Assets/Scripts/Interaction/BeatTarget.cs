using System;
using UnityEngine;

namespace RHCommunityHack.Interaction
{
    // Portable core of the ring/sphere hit mechanic (see Docs/Ring-Sphere 交互判定与美术规格.md).
    // Public surface is deliberately narrow: Initialize + TryTouch + OnResolved. Any input source
    // (keyboard test harness today, a VR controller trigger collider later) drives this through
    // TryTouch and nothing else - the judgment logic never branches on where the touch came from.
    //
    // Hierarchy contract: this component sits on a root that STAYS AT SCALE 1, with the sphere
    // visual and the ring as separate children. Scaling the root instead would multiply into the
    // ring's own scale, which is exactly what once made the ring's "perfect" radius disagree with
    // the sphere's actual radius by ~4x.
    [RequireComponent(typeof(SphereCollider))]
    public class BeatTarget : MonoBehaviour
    {
        public event Action<BeatTarget, JudgmentResult> OnResolved;

        [SerializeField] Transform sphereTransform;
        [SerializeField] Renderer sphereRenderer;
        [SerializeField] Transform ringTransform;
        [SerializeField] Renderer ringRenderer;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int RingRadiusId = Shader.PropertyToID("_RingRadius");

        BeatTargetConfig config;
        SphereCollider hitCollider;
        MaterialPropertyBlock propertyBlock;

        double spawnTimeDsp;
        double perfectTimeDsp;
        double expireTimeDsp;

        bool resolved;
        JudgmentResult? pendingVanishResult;
        double vanishStartDsp;

        // Where inside the ring quad's span the shader actually paints the visible circle.
        // Read from the material rather than hard-coded, so retuning _RingRadius in the
        // Inspector can't silently desync the drawn ring from the radius this code intends.
        float ringVisibleRadiusRatio = 1f;

        void Awake()
        {
            hitCollider = GetComponent<SphereCollider>();
            hitCollider.isTrigger = true;
        }

        public void Initialize(BeatTargetConfig targetConfig, double targetPerfectTimeDsp)
        {
            config = targetConfig;
            // dspTime is a monotonic audio clock, immune to frame-rate hitches - Time.time drift
            // is exactly what would misalign hit timing from the music over a multi-minute song.
            spawnTimeDsp = AudioSettings.dspTime;
            perfectTimeDsp = targetPerfectTimeDsp;
            expireTimeDsp = perfectTimeDsp + config.goodWindowLate;

            resolved = false;
            pendingVanishResult = null;

            CacheRingVisibleRadiusRatio();

            transform.localScale = Vector3.one;

            hitCollider.enabled = true;
            hitCollider.radius = config.sphereRadius * config.hitColliderRadiusMultiplier;

            if (sphereTransform != null)
            {
                sphereTransform.localScale = Vector3.one * config.sphereRadius * 2f;
            }

            if (ringRenderer != null) ringRenderer.enabled = true;
            ApplyRingRadius(config.ringStartRadius);
            SetAlpha(sphereRenderer, 1f);
        }

        // Returns false if this target can no longer be resolved (already hit, or already
        // vanishing) - the caller (keyboard harness or a future VR collider adapter) should
        // treat false as "this touch didn't count, try a different target."
        public bool TryTouch(double touchTimeDsp)
        {
            if (resolved || pendingVanishResult.HasValue) return false;

            double delta = touchTimeDsp - perfectTimeDsp;
            double absDelta = Math.Abs(delta);

            JudgmentResult result;
            if (absDelta <= config.perfectWindow)
            {
                result = JudgmentResult.Perfect;
            }
            else if ((delta < 0 && absDelta <= config.goodWindowEarly) ||
                     (delta > 0 && absDelta <= config.goodWindowLate))
            {
                result = JudgmentResult.Good;
            }
            else
            {
                result = JudgmentResult.MissTouch;
            }

            Debug.Log($"[BeatTarget] touch delta={delta * 1000:F0}ms -> {result} " +
                      $"(perfect=±{config.perfectWindow * 1000:F0}ms, good=-{config.goodWindowEarly * 1000:F0}/+{config.goodWindowLate * 1000:F0}ms)");

            Resolve(result);
            return true;
        }

        void Update()
        {
            double now = AudioSettings.dspTime;

            if (pendingVanishResult.HasValue)
            {
                TickVanishAnimation(now);
                return;
            }

            if (resolved) return;

            if (now >= expireTimeDsp)
            {
                Resolve(JudgmentResult.MissTimeout);
                return;
            }

            TickRingScale(now);
        }

        void TickRingScale(double now)
        {
            if (ringTransform == null) return;

            double span = perfectTimeDsp - spawnTimeDsp;
            float t = span > 0.0001 ? Mathf.Clamp01((float)((now - spawnTimeDsp) / span)) : 1f;
            float curveT = config.ringShrinkCurve.Evaluate(t);
            float radius = Mathf.Lerp(config.ringStartRadius, config.sphereRadius, curveT);
            ApplyRingRadius(radius);
        }

        // worldRadius is the radius the *visible* circle should have. The quad has to be scaled
        // larger than that, because the shader paints its circle at ringVisibleRadiusRatio of
        // the quad's half-width rather than right at its edge.
        void ApplyRingRadius(float worldRadius)
        {
            if (ringTransform == null) return;
            ringTransform.localScale = Vector3.one * (2f * worldRadius / ringVisibleRadiusRatio);
        }

        void CacheRingVisibleRadiusRatio()
        {
            ringVisibleRadiusRatio = 1f;
            if (ringRenderer == null) return;

            Material material = ringRenderer.sharedMaterial;
            if (material != null && material.HasProperty(RingRadiusId))
            {
                ringVisibleRadiusRatio = Mathf.Max(0.01f, material.GetFloat(RingRadiusId));
            }
        }

        void Resolve(JudgmentResult result)
        {
            resolved = true;
            hitCollider.enabled = false;

            if (result == JudgmentResult.Perfect || result == JudgmentResult.Good)
            {
                SpawnHitVfx(result);
                PlaySfx(result);
                OnResolved?.Invoke(this, result);
                Destroy(gameObject);
                return;
            }

            PlaySfx(result);
            pendingVanishResult = result;
            vanishStartDsp = AudioSettings.dspTime;
        }

        void TickVanishAnimation(double now)
        {
            JudgmentResult result = pendingVanishResult.Value;
            bool isMissTouch = result == JudgmentResult.MissTouch;

            float duration = isMissTouch ? config.missTouchVanishDuration : config.missTimeoutVanishDuration;
            AnimationCurve curve = isMissTouch ? config.missTouchVanishCurve : config.missTimeoutVanishCurve;
            float t = duration > 0f ? Mathf.Clamp01((float)((now - vanishStartDsp) / duration)) : 1f;
            float curveT = curve.Evaluate(t);

            if (isMissTouch)
            {
                // Touched but off-beat: swell outward and fade. Deliberately the opposite motion
                // from a beat that was never touched at all, so the two failures read differently.
                if (ringRenderer != null) ringRenderer.enabled = false;

                float scale = Mathf.Lerp(1f, config.missTouchGrowScale, curveT) * config.sphereRadius * 2f;
                if (sphereTransform != null) sphereTransform.localScale = Vector3.one * scale;
                SetAlpha(sphereRenderer, 1f - curveT);
            }
            else
            {
                float shrink = 1f - curveT;
                if (sphereTransform != null)
                {
                    sphereTransform.localScale = Vector3.one * shrink * config.sphereRadius * 2f;
                }
                ApplyRingRadius(config.sphereRadius * shrink);
            }

            if (t >= 1f)
            {
                OnResolved?.Invoke(this, result);
                Destroy(gameObject);
            }
        }

        void SpawnHitVfx(JudgmentResult result)
        {
            GameObject prefab = result == JudgmentResult.Perfect ? config.perfectVfxPrefab : config.goodVfxPrefab;
            if (prefab != null) Instantiate(prefab, transform.position, Quaternion.identity);
        }

        void PlaySfx(JudgmentResult result)
        {
            AudioClip clip = result switch
            {
                JudgmentResult.Perfect => config.perfectSfx,
                JudgmentResult.Good => config.goodSfx,
                JudgmentResult.MissTouch => config.missTouchSfx,
                JudgmentResult.MissTimeout => config.missTimeoutSfx,
                _ => null
            };
            if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position);
        }

        // Property block rather than renderer.material: no per-instance material clones leaking
        // for every beat that spawns.
        void SetAlpha(Renderer targetRenderer, float alpha)
        {
            if (targetRenderer == null) return;

            propertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);

            Color baseColor = Color.white;
            Material shared = targetRenderer.sharedMaterial;
            if (shared != null && shared.HasProperty(BaseColorId))
            {
                baseColor = shared.GetColor(BaseColorId);
            }

            baseColor.a = alpha;
            propertyBlock.SetColor(BaseColorId, baseColor);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}

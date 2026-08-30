using UnityEngine;

namespace RHCommunityHack.Interaction
{
    // An orb that lights up as a hand reaches into it. Used to show a dancer where their hands
    // should be, but it knows nothing about dance recordings - it needs its own transform and
    // two hand transforms, nothing else, so it rides anything that moves.
    //
    // This is deliberately NOT a BeatTarget. That type is organised around a single moment
    // (lead time, judgment windows, resolve, expire); this one exists continuously and has no
    // moment, no judgment and no Miss. See .ai/decisions/guide-orb-not-a-beat-target.md and
    // Docs/Guide Orb 跟随引导球规格.md
    //
    // HIERARCHY CONTRACT: this object's world scale must stay 1. orbRadius is a world-space
    // radius, and both the visual size and the activation distance are derived from it - a
    // scaled parent silently breaks that correspondence. The same trap cost a long debugging
    // session on the beat ring; see .ai/debug/2026-08-20-ring-radius-wildly-off-from-sphere-radius.md
    public class GuideOrb : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Which hand this orb belongs to. Either hand can light it up, but the other " +
                 "one reads as rejected and does not count toward the follow rate.")]
        [SerializeField] BeatHand ownerHand = BeatHand.Right;

        [Header("Hands")]
        [Tooltip("Plain Transforms, not XR types, so this stays droppable into another rig.")]
        [SerializeField] Transform leftHand;
        [SerializeField] Transform rightHand;

        [Header("Geometry")]
        [Tooltip("World-space radius in metres. Drives BOTH the visual size and the activation " +
                 "distance - never set the two independently.")]
        [SerializeField, Min(0.01f)] float orbRadius = 0.15f;

        [Header("Excite response")]
        [Tooltip("Ceiling on how far the wrong hand can drive excite.")]
        [SerializeField, Range(0f, 1f)] float wrongHandExciteCap = 0.35f;
        [Tooltip("Time constant for excite rising. Deliberately shorter than release.")]
        [SerializeField, Range(0f, 1f)] float exciteAttack = 0.08f;
        [Tooltip("Time constant for excite falling. Longer than attack, so energy lingers.")]
        [SerializeField, Range(0f, 2f)] float exciteRelease = 0.25f;

        [Header("Visuals")]
        [Tooltip("The orb mesh, on a child so this object's own scale can stay 1. Its transform " +
                 "is the thing that gets scaled - one reference, so the mesh and the object being " +
                 "resized can never drift apart.")]
        [SerializeField] Renderer orbRenderer;
        [SerializeField] Color baseColor = new Color(0.12f, 0.55f, 0.85f, 1f);
        [ColorUsage(true, true)]
        [SerializeField] Color rimColor = new Color(0.35f, 0.95f, 1f, 1f);
        [Tooltip("How far the wrong hand pushes the colour toward grey. This is what makes a " +
                 "wrong hand read as REJECTED rather than as merely weaker.")]
        [SerializeField, Range(0f, 1f)] float wrongHandDesaturation = 0.8f;
        [Tooltip("How grey the orb goes when no hand is in it. Deliberately deeper than the " +
                 "wrong-hand value: no hand is a more inert state than the wrong hand.")]
        [SerializeField, Range(0f, 1f)] float idleDesaturation = 0.9f;
        [Tooltip("Size with no hand in the orb. Below 1 the visible orb is SMALLER than its " +
                 "activation radius, so it swells to meet an approaching hand rather than " +
                 "waiting to be touched. See the note in the spec - this is deliberate.")]
        [SerializeField] float scaleIdle = 0.5f;
        [SerializeField] float scaleExcited = 1.35f;
        [SerializeField] float rimIntensityIdle = 2.5f;
        [SerializeField] float rimIntensityExcited = 6f;
        [SerializeField, Range(0f, 1f)] float coreAlphaIdle = 0.3f;
        [SerializeField, Range(0f, 1f)] float coreAlphaExcited = 0.55f;

        [Header("Trail")]
        [Tooltip("Set its Simulation Space to World and emit over DISTANCE, not time - a " +
                 "rate-over-time trail piles up in one spot whenever the orb slows down.")]
        [SerializeField] ParticleSystem trail;
        [Tooltip("Trail density per metre travelled, idle -> activated.")]
        [SerializeField] float particleRateIdle = 3f;
        [SerializeField] float particleRateExcited = 60f;
        [SerializeField] float particleSizeIdle = 0.006f;
        [SerializeField] float particleSizeExcited = 0.014f;

        [Header("Hand trail (optional)")]
        [Tooltip("Traces the PLAYER'S OWN HAND, not the orb's path, while the owning hand is " +
                 "inside. Timings and point limits live on that component.")]
        [SerializeField] HandTrail handTrail;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        static readonly int RimIntensityId = Shader.PropertyToID("_RimIntensity");
        static readonly int CoreAlphaId = Shader.PropertyToID("_CoreAlpha");
        static readonly int ExciteId = Shader.PropertyToID("_Excite");
        static readonly int ContactPointId = Shader.PropertyToID("_ContactPoint");

        // The orb at its LARGEST - orbRadius * scaleExcited - and it stays that size regardless of
        // how big the orb currently looks. Entry is a threshold, not a gradient: inside this
        // radius is "in", outside it is "out", and the sphere shrinking while idle must not
        // shrink the thing the player is aiming at.
        //
        // Note the coupling this creates: scaleExcited is now a gameplay dial, not only a visual
        // one. Changing it moves the detection boundary.
        public float ActivationRadius => orbRadius * scaleExcited;

        // The raw threshold result for the correct hand, with no smoothing. Smoothing exists to
        // make the orb look good; measuring whether the hand was actually in should not inherit
        // a 0.25s tail.
        public bool IsFollowing { get; private set; }

        MaterialPropertyBlock propertyBlock;
        float excite;
        float correctExcite;
        float wrongExcite;
        Vector3 contactPoint;
        bool warnedAboutScale;

        // World-space particles do NOT disappear when their emitter is hidden - they live out
        // their lifetime wherever they were left. Without this, the start of a replay shows the
        // previous pass's tail still hanging in the air.
        public void ClearTrail()
        {
            if (trail != null) trail.Clear(true);
            if (handTrail != null) handTrail.Clear();
        }

        void Update()
        {
            WarnIfScaled();

            float radius = ActivationRadius;
            bool correctInside = false;
            bool wrongInside = false;
            Vector3 correctPoint = transform.position;
            Vector3 wrongPoint = transform.position;

            Sample(leftHand, BeatHand.Left, radius, ref correctInside, ref correctPoint, ref wrongInside, ref wrongPoint);
            Sample(rightHand, BeatHand.Right, radius, ref correctInside, ref correctPoint, ref wrongInside, ref wrongPoint);

            IsFollowing = correctInside;

            // The judgment is binary; the smoothing below only stops the visuals popping between
            // the two states. It does not make entry gradual.
            correctExcite = Approach(correctExcite, correctInside ? 1f : 0f);
            wrongExcite = Approach(wrongExcite, wrongInside ? wrongHandExciteCap : 0f);

            // Max, not sum: with both hands inside, the correct one must win outright. Anything
            // that let the wrong hand subtract would mean using the right hand made it dimmer.
            excite = Mathf.Max(correctExcite, wrongExcite);
            contactPoint = correctExcite >= wrongExcite ? correctPoint : wrongPoint;

            // 1 when only the wrong hand is in, 0 when the correct one leads.
            float wrongness = excite > 1e-4f
                ? Mathf.Clamp01((wrongExcite - correctExcite) / excite)
                : 0f;

            // The trail follows the PLAYER'S hand, not the orb - the orb already performs the
            // recorded path, so the only new thing a line can show is what the dancer did.
            if (handTrail != null)
            {
                Transform hand = OwningHand();
                if (hand != null) handTrail.Track(hand.position, correctInside);
            }

            ApplyVisuals(wrongness);
        }

        Transform OwningHand()
        {
            if ((ownerHand & BeatHand.Right) != BeatHand.None && rightHand != null) return rightHand;
            if ((ownerHand & BeatHand.Left) != BeatHand.None && leftHand != null) return leftHand;
            return null;
        }

        void Sample(Transform hand, BeatHand which, float radius,
                    ref bool correctInside, ref Vector3 correctPoint,
                    ref bool wrongInside, ref Vector3 wrongPoint)
        {
            if (hand == null) return;

            // Squared comparison - no square root needed just to answer in-or-out.
            if ((hand.position - transform.position).sqrMagnitude > radius * radius) return;

            if ((ownerHand & which) != BeatHand.None)
            {
                correctInside = true;
                correctPoint = hand.position;
                return;
            }

            wrongInside = true;
            wrongPoint = hand.position;
        }

        // Frame-rate independent exponential approach, with a different time constant each way.
        float Approach(float current, float target)
        {
            float tau = target > current ? exciteAttack : exciteRelease;
            if (tau <= 0f) return target;
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-Time.deltaTime / tau));
        }

        void ApplyVisuals(float wrongness)
        {
            // Two separate reasons to go grey - nobody's hand, and the wrong hand. Whichever
            // pulls harder wins, so approaching with the correct hand always brightens the orb
            // and approaching with the wrong one never does.
            float greyness = Mathf.Max(idleDesaturation * (1f - excite), wrongness * wrongHandDesaturation);
            Color tintedBase = Color.Lerp(baseColor, Desaturate(baseColor), greyness);
            Color tintedRim = Color.Lerp(rimColor, Desaturate(rimColor), greyness);

            // Growth is driven by the CORRECT hand only - a wrong hand lights the orb but must
            // not make it swell, or the two states stop being distinguishable at a glance.
            if (orbRenderer != null)
            {
                float growth = Mathf.Lerp(scaleIdle, scaleExcited, correctExcite);
                orbRenderer.transform.localScale = Vector3.one * (orbRadius * 2f * growth);

                propertyBlock ??= new MaterialPropertyBlock();
                orbRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, tintedBase);
                propertyBlock.SetColor(RimColorId, tintedRim);
                propertyBlock.SetFloat(RimIntensityId, Mathf.Lerp(rimIntensityIdle, rimIntensityExcited, excite));
                propertyBlock.SetFloat(CoreAlphaId, Mathf.Lerp(coreAlphaIdle, coreAlphaExcited, excite));
                propertyBlock.SetFloat(ExciteId, excite);
                propertyBlock.SetVector(ContactPointId, contactPoint);
                orbRenderer.SetPropertyBlock(propertyBlock);
            }

            // Ahead of the particle guard below - the line does not depend on there being a
            // particle system.
            if (handTrail != null) handTrail.SetColor(tintedRim);

            if (trail == null) return;

            // Density follows the correct hand, so a wrong hand stays at the idle rate - sparser
            // than an activated orb without needing a dial of its own.
            var emission = trail.emission;
            emission.rateOverDistance = Mathf.Lerp(particleRateIdle, particleRateExcited, correctExcite);

            // startColor is deliberately NOT written here: the trail keeps whatever colour the
            // particle system was authored with, independent of the orb's state. Only the path
            // line follows the orb.
            var main = trail.main;
            main.startSize = Mathf.Lerp(particleSizeIdle, particleSizeExcited, excite);
        }

        static Color Desaturate(Color c)
        {
            float grey = c.grayscale;
            return new Color(grey, grey, grey, c.a);
        }

        void WarnIfScaled()
        {
            if (warnedAboutScale) return;

            Vector3 scale = transform.lossyScale;
            if (Mathf.Abs(scale.x - 1f) < 0.001f &&
                Mathf.Abs(scale.y - 1f) < 0.001f &&
                Mathf.Abs(scale.z - 1f) < 0.001f) return;

            warnedAboutScale = true;
            Debug.LogWarning($"[GuideOrb] World scale is {scale}, not 1. orbRadius is a world-space " +
                             "radius, so a scaled parent makes the visible orb and the activation " +
                             "distance disagree. Put this under an unscaled parent.", this);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, ActivationRadius);
        }
    }
}

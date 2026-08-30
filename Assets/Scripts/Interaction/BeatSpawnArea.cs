using System.Collections.Generic;
using UnityEngine;

namespace RHCommunityHack.Interaction
{
    // Defines the volume beats appear in. Put this on a marker object (the scene's "Anchor")
    // and point every spawner at it, so the region is described in exactly one place.
    //
    // As a BeatPlacementSource this is the "scatter beats about" mode - kept alongside the
    // recording-driven source so the judgment loop can still be exercised without a take loaded.
    public class BeatSpawnArea : BeatPlacementSource
    {
        [Tooltip("Half-extent of the random offset per axis, in world metres.")]
        [SerializeField] float scatter = 0.2f;

        public float Scatter => scatter;

        // Names no hand: a scattered beat has no business preferring one, so the spawner
        // picks the flavour itself.
        public override void GetPlacements(double perfectTimeDsp, List<BeatPlacement> into)
        {
            into.Add(new BeatPlacement { position = GetRandomPoint(), hand = BeatHand.Either });
        }

        public Vector3 GetRandomPoint()
        {
            Vector3 offset = new Vector3(
                Random.Range(-scatter, scatter),
                Random.Range(-scatter, scatter),
                Random.Range(-scatter, scatter));

            // Rotation only, deliberately NOT TransformPoint: this usually lives on a small
            // marker object (the scene Anchor is scaled 0.1), and TransformPoint would shrink
            // the region by that factor. Scatter stays in world metres whatever the marker size.
            return transform.position + transform.rotation * offset;
        }

        void OnDrawGizmosSelected()
        {
            // Same position+rotation-without-scale basis the spawning uses, so what you see
            // selected in the scene view is the region beats actually appear in.
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.color = new Color(0.35f, 0.95f, 1f, 0.9f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one * scatter * 2f);
        }
    }
}

using UnityEngine;

namespace RHCommunityHack.Interaction
{
    // Generic "always face the player" utility - not specific to the ring, reusable elsewhere.
    public class BillboardToCamera : MonoBehaviour
    {
        Camera targetCamera;

        void OnEnable()
        {
            targetCamera = Camera.main;
        }

        void LateUpdate()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null) return;
            }

            transform.rotation = Quaternion.LookRotation(transform.position - targetCamera.transform.position);
        }
    }
}

using UnityEngine;

namespace RHCommunityHack.Environment
{
    // Fades the view to black and back, for covering a teleport.
    //
    // A QUAD PARENTED TO THE CAMERA, not a Canvas. This is the whole reason the class exists:
    // a Screen Space - Overlay canvas renders nothing at all in a headset, so the obvious way to
    // build a fade produces something that works perfectly on the monitor and is simply absent in
    // VR. A world-space quad a few centimetres in front of the eyes is the version that works in
    // both, and it needs no camera stack or render feature.
    //
    // Sized in Reset() to cover a wide HMD field of view at its parking distance. Depth is off
    // and the queue is Overlay, so it covers everything regardless of what it is standing inside.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public class ScreenFade : MonoBehaviour
    {
        [Tooltip("Distance in front of the camera. Close enough that nothing can get between it " +
                 "and the eyes, far enough not to fall inside the near clip plane.")]
        [SerializeField, Min(0.02f)] float distance = 0.1f;

        [Tooltip("Edge length. At 0.1m away, 0.6 covers about 143 degrees - wider than any " +
                 "current headset, which is the point: a fade with visible corners is worse than " +
                 "no fade.")]
        [SerializeField, Min(0.05f)] float size = 0.6f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Renderer quad;
        MaterialPropertyBlock block;
        float opacity;

        public float Opacity => opacity;

        void Awake()
        {
            quad = GetComponent<Renderer>();
            Place();
            SetOpacity(0f);
        }

        void OnValidate() => Place();

        void Place()
        {
            transform.localPosition = new Vector3(0f, 0f, distance);
            transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(size, size, 1f);
        }

        // 0 is fully transparent, 1 fully black. The renderer is switched off entirely at 0 so a
        // fade that is not running costs nothing - a full-screen transparent quad in front of
        // both eyes is not free on a standalone headset.
        public void SetOpacity(float value)
        {
            opacity = Mathf.Clamp01(value);
            if (quad == null) quad = GetComponent<Renderer>();
            if (quad == null) return;

            bool visible = opacity > 0.001f;
            if (quad.enabled != visible) quad.enabled = visible;
            if (!visible) return;

            block ??= new MaterialPropertyBlock();
            quad.GetPropertyBlock(block);
            block.SetColor(BaseColorId, new Color(0f, 0f, 0f, opacity));
            quad.SetPropertyBlock(block);
        }
    }
}

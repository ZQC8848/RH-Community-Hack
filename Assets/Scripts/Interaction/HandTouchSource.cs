using UnityEngine;

namespace RHCommunityHack.Interaction
{
    // Physical-contact input adapter: put this on a small trigger volume parented to a
    // controller (or a tracked hand, or anything else that can reach out), tell it which hand
    // it represents, and it drives BeatTarget through the same public TryTouch API the
    // keyboard test harness uses.
    //
    // Deliberately references no XR types at all - it only needs a Collider and a hand label -
    // so the interaction module stays droppable into a project with a different rig.
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class HandTouchSource : MonoBehaviour
    {
        [SerializeField] BeatHand hand = BeatHand.Right;

        public BeatHand Hand => hand;

        void Awake()
        {
            // Trigger-vs-trigger contact only raises OnTriggerEnter if one side has a
            // Rigidbody; BeatTarget deliberately has none, so this side carries it.
            GetComponent<Collider>().isTrigger = true;

            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        void OnTriggerEnter(Collider other)
        {
            var target = other.GetComponentInParent<BeatTarget>();
            if (target == null) return;

            target.TryTouch(AudioSettings.dspTime, hand);
        }
    }
}

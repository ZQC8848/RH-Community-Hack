using UnityEngine;
using UnityEngine.UI;

namespace RHCommunityHack.Play
{
    // The narration subtitle, on a panel that stays where you are looking.
    //
    // NOT a Screen Space - Overlay canvas. That renders nothing at all in a headset, which is the
    // same trap ScreenFade exists to avoid - it looks perfect on the monitor and is simply absent
    // in VR. This is a world-space canvas that follows the head.
    //
    // It follows LAZILY, and that is the whole design of this class. Text welded to the head is
    // one of the most reliable ways to make someone ill: it never settles, so the eyes never get
    // to rest on it. So the panel holds still while you look around, and only slides after you
    // when your gaze has left it by more than a threshold - and then it moves smoothly rather
    // than snapping. During the prologue you are meant to be turning to look at objects appearing
    // around you, so this is the difference between readable and unusable.
    //
    // Legacy UI.Text rather than TextMeshPro, matching the rest of this project: TMP's essential
    // resources are not imported, so TMP text renders nothing at all.
    [DisallowMultipleComponent]
    public class SubtitleDisplay : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The head. Left empty it finds the main camera, which is what PlayScene wants.")]
        [SerializeField] Transform head;

        [Tooltip("Root to switch off when there is nothing to say - the panel background and the " +
                 "text together, so an empty line leaves no floating rectangle.")]
        [SerializeField] GameObject panel;

        [SerializeField] Text speakerText;
        [SerializeField] Text bodyText;

        [Header("Placement")]
        [Tooltip("Metres in front of the head. Far enough to be comfortable to focus on, near " +
                 "enough to read.")]
        [SerializeField, Min(0.5f)] float distance = 2.6f;

        [Tooltip("Metres below eye level. Subtitles sit low so they do not cover what is being " +
                 "talked about.")]
        [SerializeField] float dropBelowEye = 0.55f;

        [Header("Lazy follow")]
        [Tooltip("Degrees of head turn tolerated before the panel starts following. Below this it " +
                 "does not move at all, which is what lets the eyes settle on it.")]
        [SerializeField, Range(0f, 60f)] float deadZoneDegrees = 18f;

        [Tooltip("How quickly it catches up once it has started moving. Higher is snappier and " +
                 "less comfortable.")]
        [SerializeField, Min(0.1f)] float followSpeed = 2.2f;

        float currentYaw;
        bool following;

        public bool IsShowing => panel != null && panel.activeSelf;

        Transform Head
        {
            get
            {
                if (head != null) return head;
                var cam = Camera.main;
                if (cam != null) head = cam.transform;
                return head;
            }
        }

        void Awake()
        {
            if (Head != null) currentYaw = Head.eulerAngles.y;
            Hide();
        }

        // Empty text hides the panel rather than showing an empty box - that is how a silent beat
        // in the cue list is written.
        public void Show(string speaker, string body)
        {
            if (string.IsNullOrWhiteSpace(body)) { Hide(); return; }

            if (panel != null && !panel.activeSelf)
            {
                panel.SetActive(true);
                // Snap into place on the first frame of a new line rather than sliding in from
                // wherever the last one was left.
                if (Head != null) currentYaw = Head.eulerAngles.y;
                Place(instant: true);
            }

            if (speakerText != null) speakerText.text = speaker != null ? speaker.ToUpperInvariant() : "";
            if (bodyText != null) bodyText.text = body;
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
            following = false;
        }

        void LateUpdate()
        {
            if (!IsShowing || Head == null) return;
            Place(instant: false);
        }

        void Place(bool instant)
        {
            float headYaw = Head.eulerAngles.y;
            float error = Mathf.Abs(Mathf.DeltaAngle(currentYaw, headYaw));

            // Hysteresis on the dead zone: once it has started following it keeps following until
            // it has caught up, instead of stopping dead the moment the error dips under the
            // threshold and leaving the panel half way.
            if (error > deadZoneDegrees) following = true;
            else if (error < 1f) following = false;

            if (instant) currentYaw = headYaw;
            else if (following)
                currentYaw = Mathf.LerpAngle(currentYaw, headYaw, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));

            var rot = Quaternion.Euler(0f, currentYaw, 0f);
            Vector3 target = Head.position + rot * Vector3.forward * distance + Vector3.down * dropBelowEye;

            transform.position = instant
                ? target
                : Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
            transform.rotation = rot;
        }
    }
}

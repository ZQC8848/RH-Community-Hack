using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RHCommunityHack.Interaction.DevTesting
{
    // Editor/dev-only input adapter for iterating on judgment timing without a headset.
    // Deliberately NOT part of the portable BeatTarget module (see Docs/Ring-Sphere 交互判定与美术规格.md §8):
    // it drives BeatTarget through the exact same public TryTouch API a real VR controller
    // trigger collider would use, so swapping to real VR input later means adding a new adapter,
    // not touching this class or BeatTarget itself. Don't carry this folder into a build/port.
    //
    // Uses the Input System package (Keyboard.current), not the legacy Input class - this
    // project's Active Input Handling is set to "Input System Package", where UnityEngine.Input
    // throws InvalidOperationException at runtime.
    public class BeatTargetKeyboardTestHarness : MonoBehaviour
    {
        [SerializeField] BeatTarget beatTargetPrefab;
        [SerializeField] BeatTargetConfig config;

        [Header("Spawn placement")]
        [Tooltip("Shared spawn region (normally the scene Anchor). Leave empty to fall back to a fixed distance in front of the camera.")]
        [SerializeField] BeatSpawnArea spawnArea;
        [Tooltip("Only used when no spawn area is assigned.")]
        [SerializeField] float spawnDistance = 1.5f;

        [Header("Keys")]
        [SerializeField] Key spawnKey = Key.E;
        // Separate keys per hand so the wrong-hand rule is testable without a headset:
        // touching a right-hand-only beat with the left key should produce Miss-Touch.
        [SerializeField] Key leftHandKey = Key.Q;
        [SerializeField] Key rightHandKey = Key.Space;

        readonly List<BeatTarget> activeTargets = new List<BeatTarget>();

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[spawnKey].wasPressedThisFrame) SpawnTarget();
            if (keyboard[leftHandKey].wasPressedThisFrame) TouchOldest(BeatHand.Left);
            if (keyboard[rightHandKey].wasPressedThisFrame) TouchOldest(BeatHand.Right);
        }

        void SpawnTarget()
        {
            if (beatTargetPrefab == null || config == null)
            {
                Debug.LogWarning("BeatTargetKeyboardTestHarness is missing its prefab or config reference.");
                return;
            }

            if (!TryGetSpawnPosition(out Vector3 spawnPosition)) return;

            BeatTarget instance = Instantiate(beatTargetPrefab, spawnPosition, Quaternion.identity);
            double perfectTime = AudioSettings.dspTime + config.ringLeadTime;
            instance.Initialize(config, perfectTime);
            instance.OnResolved += HandleResolved;
            activeTargets.Add(instance);
        }

        bool TryGetSpawnPosition(out Vector3 position)
        {
            if (spawnArea != null)
            {
                position = spawnArea.GetRandomPoint();
                return true;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("BeatTargetKeyboardTestHarness has no spawn area and no Camera.main to fall back to.");
                position = default;
                return false;
            }

            position = cam.transform.position + cam.transform.forward * spawnDistance;
            return true;
        }

        void TouchOldest(BeatHand hand)
        {
            activeTargets.RemoveAll(t => t == null);
            if (activeTargets.Count == 0) return;

            // Don't remove here: Perfect/Good resolve synchronously inside TryTouch and
            // already fire OnResolved -> HandleResolved -> activeTargets.Remove before this
            // call returns. Removing again (by index) after the fact raced with that removal.
            activeTargets[0].TryTouch(AudioSettings.dspTime, hand);
        }

        void HandleResolved(BeatTarget target, JudgmentResult result)
        {
            Debug.Log($"[BeatTargetTest] {result}");
            activeTargets.Remove(target);
        }
    }
}

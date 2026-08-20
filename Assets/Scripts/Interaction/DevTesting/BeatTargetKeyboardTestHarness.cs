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
        [SerializeField] float spawnDistance = 1.5f;
        [SerializeField] Key spawnKey = Key.E;
        [SerializeField] Key touchKey = Key.Space;

        readonly List<BeatTarget> activeTargets = new List<BeatTarget>();

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[spawnKey].wasPressedThisFrame) SpawnTarget();
            if (keyboard[touchKey].wasPressedThisFrame) TouchOldest();
        }

        void SpawnTarget()
        {
            if (beatTargetPrefab == null || config == null)
            {
                Debug.LogWarning("BeatTargetKeyboardTestHarness is missing its prefab or config reference.");
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("BeatTargetKeyboardTestHarness needs a Camera.main to spawn in front of.");
                return;
            }

            Vector3 spawnPosition = cam.transform.position + cam.transform.forward * spawnDistance;
            BeatTarget instance = Instantiate(beatTargetPrefab, spawnPosition, Quaternion.identity);
            double perfectTime = AudioSettings.dspTime + config.ringLeadTime;
            instance.Initialize(config, perfectTime);
            instance.OnResolved += HandleResolved;
            activeTargets.Add(instance);
        }

        void TouchOldest()
        {
            activeTargets.RemoveAll(t => t == null);
            if (activeTargets.Count == 0) return;

            // Don't remove here: Perfect/Good resolve synchronously inside TryTouch and
            // already fire OnResolved -> HandleResolved -> activeTargets.Remove before this
            // call returns. Removing again (by index) after the fact raced with that removal.
            activeTargets[0].TryTouch(AudioSettings.dspTime);
        }

        void HandleResolved(BeatTarget target, JudgmentResult result)
        {
            Debug.Log($"[BeatTargetTest] {result}");
            activeTargets.Remove(target);
        }
    }
}
